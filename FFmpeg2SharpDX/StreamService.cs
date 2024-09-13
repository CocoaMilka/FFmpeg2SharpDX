using System;
using System.Runtime.InteropServices;

using FFmpegInteropX;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

using Windows.UI.Xaml.Controls;
using SharpDX.Direct3D;

using Windows.Graphics.Display;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;
using System.Diagnostics;

using Microsoft.UI.Xaml;
using SharpDX.Mathematics.Interop;
using System.Threading.Tasks;

namespace FFmpeg2SharpDX
{
    [ComImport, Guid("790a45f7-0d42-4876-983a-0a55cfe6f4aa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISwapChainPanelNative
    {
        void SetSwapChain(IntPtr swapChainPointer);
    }


    public class StreamService
    {
        private SwapChainPanel _swapChainPanel;
        private SharpDX.Direct3D11.Device device;
        private SharpDX.Direct3D11.DeviceContext deviceContext;
        private SwapChain swapChain;
        private IDirect3DSurface surface;

        private RenderTargetView renderTargetView;
        private Texture2D renderTargetTexture;
        private Texture2D backBuffer;

        // Media player members
        private MediaPlayer mediaPlayer;
        private MediaPlaybackItem mediaPlaybackItem;
        private FFmpegMediaSource ffmpegMediaSource;
        private SoftwareBitmap frameServerDest = new SoftwareBitmap(BitmapPixelFormat.Rgba8, 512, 512, BitmapAlphaMode.Premultiplied);
        private IDirect3DSurface direct3DSurface;


        public StreamService(SwapChainPanel swapChainPanel)
        {
            _swapChainPanel = swapChainPanel;

            swapChainPanel.Loaded += (sender, args) =>
            {
                // Only create the swap chain after the SwapChainPanel is loaded
                InitializeSwapChain();
                InitializeMediaPlayer();
            };
        }

        private void InitializeSwapChain()
        {
            // Create Direct3D device and context
            var creationFlags = DeviceCreationFlags.BgraSupport;
            creationFlags |= DeviceCreationFlags.Debug;
            device = new Device(SharpDX.Direct3D.DriverType.Hardware, creationFlags);

            // Verify valid dimensions
            var width = (int)_swapChainPanel.ActualWidth;
            var height = (int)_swapChainPanel.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("SwapChainPanel size is invalid.");
            }

            if (_swapChainPanel == null)
            {
                throw new InvalidOperationException("SwapChainPanel is not initialized.");
            }

            // Descriptions
            SwapChainDescription1 swapChainDesc = new SwapChainDescription1()
            {
                Width = (int)_swapChainPanel.ActualWidth,
                Height = (int)_swapChainPanel.ActualHeight,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.BackBuffer | Usage.RenderTargetOutput,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipSequential,
                Scaling = Scaling.Stretch,
                AlphaMode = AlphaMode.Premultiplied,
                Flags = SwapChainFlags.AllowModeSwitch | SwapChainFlags.AllowTearing
            };

            var textureDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            };

            using (var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>())
            {
                using (var adapter = dxgiDevice.Adapter)
                {
                    using (var factory = adapter.GetParent<Factory2>())
                    {
                        // Create the swap chain
                        swapChain = new SwapChain1(factory, dxgiDevice, ref swapChainDesc);

                        // Link swap chain to the SwapChainPanel (if using UWP/XAML)
                        var swapChainPanel = this._swapChainPanel;
                        var swapChainPanelNative = ComObject.As<SharpDX.DXGI.ISwapChainPanelNative>(swapChainPanel);
                        swapChainPanelNative.SwapChain = swapChain;

                        // Get the back buffer from the swap chain
                        backBuffer = swapChain.GetBackBuffer<Texture2D>(0);

                        // Create the render target view for the back buffer
                        renderTargetView = new RenderTargetView(device, backBuffer);

                        // Set the back buffer as the render target for the output merger stage
                        device.ImmediateContext.OutputMerger.SetRenderTargets(renderTargetView);

                        // Create a Direct3DSurface from the back buffer
                        direct3DSurface = Direct3D11Helper.CreateDirect3DSurfaceFromSharpDXTexture(backBuffer);

                        // Set the viewport for the rendering area
                        var viewport = new SharpDX.Mathematics.Interop.RawViewportF
                        {
                            Width = swapChainDesc.Width,
                            Height = swapChainDesc.Height,
                            MinDepth = 0.0f,
                            MaxDepth = 1.0f
                        };
                        device.ImmediateContext.Rasterizer.SetViewport(viewport);

                        // Now direct3DSurface is tied to the back buffer
                        // FFmpegInteropX or other sources can render to the swap chain's back buffer via this surface
                    }
                }
            }

        }

        private async void InitializeMediaPlayer()
        {
            try
            {
                FFmpegInteropLogging.SetDefaultLogProvider();

                MediaSourceConfig configuration = new MediaSourceConfig()
                {
                    MaxVideoThreads = 8,
                    SkipErrors = uint.MaxValue,
                    //ReadAheadBufferDuration = TimeSpan.Zero,
                    FastSeek = true,
                    VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder
                };

                // Sample stream source
                string uri = "https://test-videos.co.uk/vids/sintel/mp4/h264/720/Sintel_720_10s_1MB.mp4";
                //string uri = "udp://@192.168.10.1:11111";

                // Create FFmpegMediaSource from sample stream
                System.Diagnostics.Debug.WriteLine($"Attempting to create media source from URI: {uri}");
                ffmpegMediaSource = await FFmpegMediaSource.CreateFromUriAsync(uri, configuration);

                // Ensure video stream is valid, display video stream information for debug
                if (ffmpegMediaSource.CurrentVideoStream == null) { throw new Exception("CurrentVideoStream is null."); }

                // Create MediaPlaybackItem from FFmpegMediaSource
                mediaPlaybackItem = ffmpegMediaSource.CreateMediaPlaybackItem();
                if (mediaPlaybackItem == null) { throw new Exception("Failed to create MediaPlaybackItem."); }

                mediaPlayer = new MediaPlayer
                {
                    Source = mediaPlaybackItem,
                    IsVideoFrameServerEnabled = true
                };

                mediaPlayer.VideoFrameAvailable += MediaPlayer_VideoFrameAvailable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception while setting up FFmpegInteropX: {ex.Message}");
            }
        }

        private async void MediaPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
        {
            try
            {
                var clearColor = new SharpDX.Mathematics.Interop.RawColor4(0.1f, 0.0f, 0.2f, 1.0f);
                device.ImmediateContext.ClearRenderTargetView(renderTargetView, clearColor);


                sender.CopyFrameToVideoSurface(direct3DSurface);

                // Create a Bitmap from the Direct3D surface
                //var frameBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(direct3DSurface);

                // Save the bitmap to a file for debug
                //await SaveSoftwareBitmapToFileAsync(frameBitmap, "frame.png");

                swapChain.Present(1, PresentFlags.None);

                System.Diagnostics.Debug.WriteLine("Frame Available");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during VideoFrameAvailable: {ex.Message}");
            }
        }

        public void RenderFrame()
        {
            if (swapChain == null || renderTargetView == null || renderTargetTexture == null)
            {
                System.Diagnostics.Debug.WriteLine("SwapChain, RenderTargetView, or RenderTargetTexture is not initialized.");
                return;
            }

            try
            {

                // Clear the render target
                device.ImmediateContext.ClearRenderTargetView(renderTargetView, new RawColor4(1, 1, 1, 1));

                // Set the render target
                device.ImmediateContext.OutputMerger.SetRenderTargets(renderTargetView);

                // Bind the texture that was updated by MediaPlayer to the pixel shader
                using (var shaderResourceView = new ShaderResourceView(device, renderTargetTexture))
                {
                    device.ImmediateContext.PixelShader.SetShaderResource(0, shaderResourceView);

                    // Render the quad or whatever geometry is required
                    device.ImmediateContext.Draw(6, 0); // Assuming full-screen quad
                }


                // Present the frame
                swapChain.Present(1, PresentFlags.None);
                System.Diagnostics.Debug.WriteLine("Frame presented successfully.");
            }
            catch (SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceRemoved || ex.ResultCode == SharpDX.DXGI.ResultCode.DeviceReset)
            {
                System.Diagnostics.Debug.WriteLine("Device removed or reset, reinitializing...");

                // Handle device removal by reinitializing the device and related resources
                InitializeSwapChain();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during RenderFrame: {ex.Message}");
            }
        }

        private async Task SaveSoftwareBitmapToFileAsync(SoftwareBitmap softwareBitmap, string fileName)
        {
            var file = await Windows.Storage.KnownFolders.PicturesLibrary.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

            using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);

                encoder.SetSoftwareBitmap(softwareBitmap);
                await encoder.FlushAsync();
            }
        }

        public void StartStream()
        {
            mediaPlayer.Play();
            System.Diagnostics.Debug.WriteLine("Streaming has started!");
        }

        public void StopStream()
        {
            mediaPlayer.Pause();
            System.Diagnostics.Debug.WriteLine("Streaming has stopped!");
        }
    }
}
using System;
using System.Runtime.InteropServices;

using FFmpegInteropX;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using Windows.UI.Xaml.Controls;
using SharpDX.Direct3D;

using Windows.Graphics.Display;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;
using System.Diagnostics;
using Windows.UI.Xaml.Media.Imaging;

namespace FFmpeg2SharpDX
{
    public class StreamService
    {
        private SwapChainPanel _swapChainPanel;
        private SharpDX.Direct3D11.Device device;
        private SharpDX.Direct3D11.DeviceContext deviceContext;
        private SwapChain swapChain;
        private RenderTargetView renderTargetView;
        private Texture2D renderTargetTexture;

        // Media player members
        private MediaPlayer mediaPlayer;
        private MediaPlaybackItem mediaPlaybackItem;
        private FFmpegMediaSource ffmpegMediaSource;
        private SoftwareBitmap frameServerDest = new SoftwareBitmap(BitmapPixelFormat.Rgba8, 512, 512, BitmapAlphaMode.Premultiplied);
        private IDirect3DSurface direct3DSurface;


        public StreamService(SwapChainPanel swapChainPanel)
        {
            _swapChainPanel = swapChainPanel;
            InitializeSwapChain();
            InitializeMediaPlayer();
            CreateRenderTarget();
        }

        private void InitializeSwapChain()
        {
            // Create device and context
            device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            deviceContext = device.ImmediateContext;

            // Get the DPI of the display
            float dpi = DisplayInformation.GetForCurrentView().LogicalDpi;

            // Calculate the swap chain panel dimensions
            int panelWidth = (int)(_swapChainPanel.ActualWidth * dpi / 96.0f);
            int panelHeight = (int)(_swapChainPanel.ActualHeight * dpi / 96.0f);

            // Initialize the swap chain description
            var swapChainDescription = new SwapChainDescription1
            {
                Width = panelWidth,
                Height = panelHeight,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipSequential,
                Scaling = Scaling.Stretch,
                AlphaMode = AlphaMode.Ignore
            };

            // Get the DXGI factory
            using (var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>())
            {
                using (var dxgiAdapter = dxgiDevice.Adapter)
                {
                    using (var dxgiFactory = dxgiAdapter.GetParent<Factory2>())
                    {
                        // Create the swap chain
                        swapChain = new SwapChain1(dxgiFactory, device, ref swapChainDescription);
                    }
                }
            }

            // Associate swap chain with the SwapChainPanel
            using (var nativePanel = ComObject.As<SharpDX.DXGI.ISwapChainPanelNative>(_swapChainPanel))
            {
                nativePanel.SwapChain = swapChain;
            }

            // Create render target view
            using (var backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
            {
                renderTargetView = new RenderTargetView(device, backBuffer);
            }

            deviceContext.OutputMerger.SetRenderTargets(renderTargetView);

            // Bind the render target view to the pipeline
            deviceContext.OutputMerger.SetRenderTargets(renderTargetView);

            System.Diagnostics.Debug.WriteLine("SwapChain initialized!");
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
                //mediaPlayer.CopyFrameToVideoSurface(renderTargetTexture);

                System.Diagnostics.Debug.WriteLine("Frame Available");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception during VideoFrameAvailable: {ex.Message}");
            }
        }

        private void CreateRenderTarget()
        {
            // Create a texture that matches the video frame size
            renderTargetTexture = new Texture2D(device, new Texture2DDescription
            {
                Width = (int)_swapChainPanel.ActualWidth,
                Height = (int)_swapChainPanel.ActualHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
            });
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
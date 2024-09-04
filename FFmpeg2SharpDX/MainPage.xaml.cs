using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FFmpeg2SharpDX
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private StreamService _streamService;
        private bool _isRendering;

        public MainPage()
        {
            this.InitializeComponent();
            _streamService = new StreamService(SwapChainPanel);
        }

        // Button events
        private void StartStream_Btn_Click(object sender, RoutedEventArgs e)
        {
            _streamService.StartStream();
            _isRendering = true;
        }

        private void StopStream_Btn_Click(object sender, RoutedEventArgs e)
        {
            _streamService.StopStream();
            _isRendering = false;
        }

        private async void RenderLoop()
        {
            while (_isRendering)
            {
                _streamService.RenderFrame();
                await Task.Delay(16); // Approximately 60 FPS
            }
        }

        private void Render_Btn_OnClick(object sender, RoutedEventArgs e)
        {
            RenderLoop();
        }
    }
}

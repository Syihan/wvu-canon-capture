using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// added manually
using System.Threading;
using System.Drawing;
using EOSDigital.API;
using EOSDigital.SDK;
using EDSDKLib;
using System.IO;
using Newtonsoft.Json;

namespace WVU_Canon_Capture
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {



        #region Startup
        // ================================================================== STARTUP ================================================================== //

        public MainWindow()
        {

            // initial startup
            // DisplaySplashScreen();
            InitializeComponent();

            // initializes the API and retrieves the list of connected cameras
            API = new CanonAPI();
            LoadCameraList();
            LoadCameraProfiles();
            LoadCollectionProfiles();

            // registers error event handlers
            ErrorHandler.SevereErrorHappened += ErrorHandler_SevereErrorHappened;
            ErrorHandler.NonSevereErrorHappened += ErrorHandler_NonSevereErrorHappened;
        }


        /// <summary>
        /// displays the splash screen before startup
        /// </summary>
        private void DisplaySplashScreen()
        {

            SplashScreen splash = new SplashScreen(@"Resources\splash_screen.png");
            splash.Show(true);
            Thread.Sleep(0);  // TODO: MAKE SPLASH SCREEN LAST LONGER

        }


        #endregion



        #region Camera and Live View
        // ================================================================== CAMERA AND LIVE VIEW ================================================================== //

        
        public CanonAPI API;                                    // the class that mainly handles the SDK lifetime, the connected Cameras and the SDK events
        public Camera MainCamera;                               // the main camera
        private List<Camera> CameraList;                        // the list of cameras available for connection
        private ImageBrush LiveViewBrush = new ImageBrush();    // the brush to paint the Live View canvas
        private Action<BitmapImage> SetImageAction;             // the accumulation of Live View images


        /// <summary>
        /// Initializes the camera list
        /// </summary>
        private void LoadCameraList()
        {
            // clears list of cameras
            this.Dispatcher.Invoke(() =>
            {
                for(int i=CameraComboBox.Items.Count-1; i>0; i--)
                {
                    CameraComboBox.Items.RemoveAt(i);
                }
            });

            // retrieves all Canon cameras connected to the computer
            API.CameraAdded += API_CameraAdded;
            CameraList = API.GetCameraList();

            // populates the Camera ComboBox with the list
            foreach (Camera cameraOption in CameraList)
                this.Dispatcher.Invoke(() => { CameraComboBox.Items.Add(cameraOption.DeviceName); });

            // TODO: Analyze whether this is the right thing to do
            // honestly not sure what this does
            //if (MainCamera?.SessionOpen == true)
            //    this.Dispatcher.Invoke(() => { CameraComboBox.SelectedIndex = CameraList.FindIndex(t => t.ID == MainCamera.ID); });
            //else if (CameraList.Count > 0) CameraComboBox.SelectedIndex = 0;
            //else
            //{
            //    MessageBox.Show("Fix this.");
            //    //LockUINoCamera();
            //}

        }


        /// <summary>
        /// Toggles the live view on/off
        /// </summary>
        private void SetLiveViewOn(Boolean on)
        {
            // if ON is true, resume live view
            // if ON is false, close the camera session
            if (on)
            {
                // closes any already open camera session
                MainCamera?.CloseSession();

                // selects the camera chosen in the Camera ComboBox and opens a new session
                MainCamera = CameraList[CameraComboBox.SelectedIndex - 1];

                // opens a brand new camera session
                MainCamera.OpenSession();

                // sets up the LiveViewCanvas
                SetImageAction = (BitmapImage img) => { LiveViewBrush.ImageSource = img; };

                // paints the LiveViewCanvas
                HomeLiveViewCanvas.Background = LiveViewBrush;
                CameraLiveViewCanvas.Background = LiveViewBrush;
                MainCamera.StartLiveView();
                ToggleLiveViewHomeButton.Content = "\u23f8";

                // displays the current camera settings
                FStopLabel.Content = AvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Av)).StringValue;
                ExposureLabel.Content = TvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Tv)).StringValue;
                ISOLabel.Content = ISOValues.GetValue(MainCamera.GetInt32Setting(PropertyID.ISO)).StringValue;

                // TODO: DETERMINE IF THE BELOW THREE LINES ARE ACTUALLY NECESSARY
                // sets up camera event handlers
                MainCamera.LiveViewUpdated += MainCamera_LiveViewUpdated;
                MainCamera.ProgressChanged += MainCamera_ProgressChanged;
                MainCamera.DownloadReady += MainCamera_DownloadReady;
            }
            else
            {
                try
                {
                    // paints the LiveView black
                    HomeLiveViewCanvas.Background = System.Windows.Media.Brushes.Transparent;
                    CameraLiveViewCanvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
                    if (ToggleLiveViewHomeButton != null)
                        ToggleLiveViewHomeButton.Content = "\u25b6";

                    // closes the camera session
                    MainCamera?.StopLiveView();
                    MainCamera?.CloseSession();
                }
                catch (Exception ex) { }

                // displays N/A for the current camera settings
                if(FStopLabel != null)
                    FStopLabel.Content = "N/A";
                if (ExposureLabel != null)
                    ExposureLabel.Content = "N/A";
                if (ISOLabel != null)
                    ISOLabel.Content = "N/A";
            }
        }


        /// <summary>
        /// Closes an existing camera session
        /// </summary>
        private void CloseCameraSession()
        {
            MainCamera?.CloseSession();
        }


        /// <summary>
        /// Focuses the camera
        /// </summary>
        private void FocusCamera()
        {
            // TODO: implement focus camera
        }


        /// <summary>
        /// Takes picture
        /// </summary>
        private void TakePicture()
        {
            // TODO: implement taking picture
        }


        /// <summary>
        /// Toggles the crosshairs on/off
        /// </summary>
        private void ToggleCrosshairs()
        {
            //TODO: toggle crosshairs
        }



        /// <summary>
        /// Sets the camera settings 
        /// </summary>
        /// <param name="fstop"></param>
        /// <param name="exposure"></param>
        /// <param name="iso"></param>
        /// <param name="wb"></param>
        private void SetCameraSettings(string fstop, string exposure, string iso, string wb)
        {

        }


        /// <summary>
        /// Initializes the list of camera settings within the Canon API
        /// </summary>
        private void InitializeCameraSettings()
        {
            CameraValue[] AvList;   // the list of f-stop/aperture values
            CameraValue[] TvList;   // the list of shutter speed/exposure values
            CameraValue[] ISOList;  // the list of iso values
            WhiteBalance[] WBList;  // the list of white balance values

            // clears the values in the comboboxes
            FStopComboBox.Items.Clear();
            ExposureComboBox.Items.Clear();
            ISOComboBox.Items.Clear();
            WhiteBalanceComboBox.Items.Clear();

            // retrieves list of possible fstop, exposure, and iso settings
            AvList = MainCamera.GetSettingsList(PropertyID.Av);
            TvList = MainCamera.GetSettingsList(PropertyID.Tv);
            ISOList = MainCamera.GetSettingsList(PropertyID.ISO);
            
            // retrieves list of all possible white balance settings (other presets cannot be set, for some reason)
            WBList = new WhiteBalance[20]
            {
                WhiteBalance.Pasted,
                WhiteBalance.Click,
                WhiteBalance.Auto,
                WhiteBalance.Daylight,
                WhiteBalance.Cloudy,
                WhiteBalance.Tungsten,
                WhiteBalance.Fluorescent,
                WhiteBalance.Strobe,
                WhiteBalance.WhitePaper,
                WhiteBalance.Shade,
                WhiteBalance.ColorTemperature,
                WhiteBalance.PCSet1,
                WhiteBalance.PCSet2,
                WhiteBalance.PCSet3,
                WhiteBalance.WhitePaper2,
                WhiteBalance.WhitePaper3,
                WhiteBalance.WhitePaper4,
                WhiteBalance.WhitePaper5,
                WhiteBalance.PCSet4,
                WhiteBalance.PCSet5
            };

            // inserts all options into the comboboxes
            foreach (var Av in AvList) FStopComboBox.Items.Add(Av.StringValue);
            foreach (var Tv in TvList) ExposureComboBox.Items.Add(Tv.StringValue);
            foreach (var ISO in ISOList) ISOComboBox.Items.Add(ISO.StringValue);
            foreach (var WB in WBList) WhiteBalanceComboBox.Items.Add(WB);
        }


        /// <summary>
        /// Event handler that detects when a new camera has been connected
        /// </summary>
        /// <param name="sender"></param>
        private void API_CameraAdded(CanonAPI sender)
        {
            LoadCameraList();
        }


        /// <summary>
        /// Event handler to continously update the LiveView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="img"></param>
        private void MainCamera_LiveViewUpdated(Camera sender, Stream img)
        {

            try
            {
                using (WrapStream memory = new WrapStream(img))
                {
                    img.Position = 0;
                    BitmapImage EvfImage = new BitmapImage();
                    EvfImage.BeginInit();
                    EvfImage.StreamSource = memory;
                    EvfImage.CacheOption = BitmapCacheOption.OnLoad;
                    EvfImage.EndInit();
                    EvfImage.Freeze();
                    Application.Current.Dispatcher.BeginInvoke(SetImageAction, EvfImage);
                }
            }
            catch (Exception ex) { ShowMessage("red", "ERROR", ex.Message); }

        }


        /// <summary>
        /// Event handler triggered by the camera's progress changing; updates the MainProgressBar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="progress"></param>
        private void MainCamera_ProgressChanged(object sender, int progress)
        {
            try { MainProgressBar.Dispatcher.Invoke((Action)delegate { MainProgressBar.Value = progress; }); }
            catch (Exception ex) { ShowMessage("red", "ERROR", ex.Message); }
        }


        /// <summary>
        /// Event handler for when the camera is ready to download an image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="Info"></param>
        private void MainCamera_DownloadReady(Camera sender, DownloadInfo Info)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// Sets f-stop/aperture (Av) settings
        /// </summary>
        /// <param name="value"></param>
        public void SetFStop(string setting)
        {
            switch (setting)
            {
                case "1":
                    MainCamera.SetSetting(PropertyID.Av, 0x08);
                    break;
                case "1.1":
                    MainCamera.SetSetting(PropertyID.Av, 0x0B);
                    break;
                case "1.2":
                    MainCamera.SetSetting(PropertyID.Av, 0x0C);
                    break;
                case "1.2 (1/3)":
                    MainCamera.SetSetting(PropertyID.Av, 0x0D);
                    break;
                case "1.4":
                    MainCamera.SetSetting(PropertyID.Av, 0x10);
                    break;
                case "1.6":
                    MainCamera.SetSetting(PropertyID.Av, 0x13);
                    break;
                case "1.8":
                    MainCamera.SetSetting(PropertyID.Av, 0x14);
                    break;
                case "1.8 (1/3)":
                    MainCamera.SetSetting(PropertyID.Av, 0x15);
                    break;
                case "2":
                    MainCamera.SetSetting(PropertyID.Av, 0x18);
                    break;
                case "22.2":
                    MainCamera.SetSetting(PropertyID.Av, 0x1B);
                    break;
                case "2.5":
                    MainCamera.SetSetting(PropertyID.Av, 0x1C);
                    break;
                case "2.5 (1/3)":
                    MainCamera.SetSetting(PropertyID.Av, 0x1D);
                    break;
                case "2.8":
                    MainCamera.SetSetting(PropertyID.Av, 0x20);
                    break;
                case "3.2":
                    MainCamera.SetSetting(PropertyID.Av, 0x23);
                    break;
                case "3.5":
                    MainCamera.SetSetting(PropertyID.Av, 0x24);
                    break;
                case "3.5 (1/3)":
                    MainCamera.SetSetting(PropertyID.Av, 0x25);
                    break;
                case "4":
                    MainCamera.SetSetting(PropertyID.Av, 0x28);
                    break;
                case "4.5":
                    MainCamera.SetSetting(PropertyID.Av, 0x2B);
                    break;
                case "4.5 (1/3)":
                    MainCamera.SetSetting(PropertyID.Av, 0x2C);
                    break;
                case "5.0":
                    MainCamera.SetSetting(PropertyID.Av, 0x2D);
                    break;
                case "5.6":
                    MainCamera.SetSetting(PropertyID.Av, 0x30);
                    break;
                case "6.3":
                    MainCamera.SetSetting(PropertyID.Av, 0x33);
                    break;
                case "6.7":
                    MainCamera.SetSetting(PropertyID.Av, 0x34);
                    break;
                case "7.1":
                    MainCamera.SetSetting(PropertyID.Av, 0x35);
                    break;
                case "8":
                    MainCamera.SetSetting(PropertyID.Av, 0x38);
                    break;
                case "9":
                    MainCamera.SetSetting(PropertyID.Av, 0x3B);
                    break;
                case "9.5":
                    MainCamera.SetSetting(PropertyID.Av, 0x3C);
                    break;
                case "10":
                    MainCamera.SetSetting(PropertyID.Av, 0x3D);
                    break;
                case "11":
                    MainCamera.SetSetting(PropertyID.Av, 0x40);
                    break;
                case "13 (1/3)":
                    MainCamera.SetSetting(PropertyID.Av, 0x43);
                    break;
                case "13":
                    MainCamera.SetSetting(PropertyID.Av, 0x44);
                    break;
                case "14":
                    MainCamera.SetSetting(PropertyID.Av, 0x45);
                    break;
                case "16":
                    MainCamera.SetSetting(PropertyID.Av, 0x48);
                    break;
                case "18":
                    MainCamera.SetSetting(PropertyID.Av, 0x4B);
                    break;
                case "19":
                    MainCamera.SetSetting(PropertyID.Av, 0x4C);
                    break;
                case "20":
                    MainCamera.SetSetting(PropertyID.Av, 0x4D);
                    break;
                case "22":
                    MainCamera.SetSetting(PropertyID.Av, 0x50);
                    break;
                case "25":
                    MainCamera.SetSetting(PropertyID.Av, 0x53);
                    break;
                case "27":
                    MainCamera.SetSetting(PropertyID.Av, 0x54);
                    break;
                case "29":
                    MainCamera.SetSetting(PropertyID.Av, 0x55);
                    break;
                case "32":
                    MainCamera.SetSetting(PropertyID.Av, 0x58);
                    break;
                case "36":
                    MainCamera.SetSetting(PropertyID.Av, 0x5B);
                    break;
                case "38":
                    MainCamera.SetSetting(PropertyID.Av, 0x5C);
                    break;
                case "40":
                    MainCamera.SetSetting(PropertyID.Av, 0x5D);
                    break;
                case "45":
                    MainCamera.SetSetting(PropertyID.Av, 0x60);
                    break;
                case "51":
                    MainCamera.SetSetting(PropertyID.Av, 0x63);
                    break;
                case "54":
                    MainCamera.SetSetting(PropertyID.Av, 0x64);
                    break;
                case "57":
                    MainCamera.SetSetting(PropertyID.Av, 0x65);
                    break;
                case "64":
                    MainCamera.SetSetting(PropertyID.Av, 0x68);
                    break;
                case "72":
                    MainCamera.SetSetting(PropertyID.Av, 0x6B);
                    break;
                case "76":
                    MainCamera.SetSetting(PropertyID.Av, 0x6C);
                    break;
                case "80":
                    MainCamera.SetSetting(PropertyID.Av, 0x6D);
                    break;
                case "91":
                    MainCamera.SetSetting(PropertyID.Av, 0x70);
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// Sets exposure/shutter speed (tv) settings
        /// </summary>
        /// <param name="value"></param>
        public void SetExposure(string setting)
        {
            switch (setting)
            {
                case "30\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x10);
                    break;
                case "25\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x13);
                    break;
                case "20\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x14);
                    break;
                case "20\" (1/3)":
                    MainCamera.SetSetting(PropertyID.Tv, 0x15);
                    break;
                case "15\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x18);
                    break;
                case "13\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x1B);
                    break;
                case "10\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x1C);
                    break;
                case "10\" (1/3)":
                    MainCamera.SetSetting(PropertyID.Tv, 0x1D);
                    break;
                case "8\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x20);
                    break;
                case "6\" (1/3)":
                    MainCamera.SetSetting(PropertyID.Tv, 0x23);
                    break;
                case "6\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x24);
                    break;
                case "5\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x25);
                    break;
                case "4\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x28);
                    break;
                case "3\"2":
                    MainCamera.SetSetting(PropertyID.Tv, 0x2B);
                    break;
                case "3\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x2C);
                    break;
                case "2\"5":
                    MainCamera.SetSetting(PropertyID.Tv, 0x2D);
                    break;
                case "2\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x30);
                    break;
                case "1\"6":
                    MainCamera.SetSetting(PropertyID.Tv, 0x33);
                    break;
                case "1\"5":
                    MainCamera.SetSetting(PropertyID.Tv, 0x34);
                    break;
                case "1\"3":
                    MainCamera.SetSetting(PropertyID.Tv, 0x35);
                    break;
                case "1\"":
                    MainCamera.SetSetting(PropertyID.Tv, 0x38);
                    break;
                case "0\"8":
                    MainCamera.SetSetting(PropertyID.Tv, 0x3B);
                    break;
                case "0\"7":
                    MainCamera.SetSetting(PropertyID.Tv, 0x3C);
                    break;
                case "0\"6":
                    MainCamera.SetSetting(PropertyID.Tv, 0x3D);
                    break;
                case "0\"5":
                    MainCamera.SetSetting(PropertyID.Tv, 0x40);
                    break;
                case "0\"4":
                    MainCamera.SetSetting(PropertyID.Tv, 0x43);
                    break;
                case "0\"3":
                    MainCamera.SetSetting(PropertyID.Tv, 0x44);
                    break;
                case "0\"3 (1/3)":
                    MainCamera.SetSetting(PropertyID.Tv, 0x45);
                    break;
                case "1/4":
                    MainCamera.SetSetting(PropertyID.Tv, 0x48);
                    break;
                case "1/5":
                    MainCamera.SetSetting(PropertyID.Tv, 0x4B);
                    break;
                case "1/6":
                    MainCamera.SetSetting(PropertyID.Tv, 0x4C);
                    break;
                case "1/6 (1/3)":
                    MainCamera.SetSetting(PropertyID.Tv, 0x4D);
                    break;
                case "1/8":
                    MainCamera.SetSetting(PropertyID.Tv, 0x50);
                    break;
                case "1/10 (1/3)":
                    MainCamera.SetSetting(PropertyID.Tv, 0x53);
                    break;
                case "1/10":
                    MainCamera.SetSetting(PropertyID.Tv, 0x54);
                    break;
                case "1/13":
                    MainCamera.SetSetting(PropertyID.Tv, 0x55);
                    break;
                case "1/15":
                    MainCamera.SetSetting(PropertyID.Tv, 0x58);
                    break;
                case "1/20 (1/3)":
                    MainCamera.SetSetting(PropertyID.Tv, 0x5B);
                    break;
                case "1/20":
                    MainCamera.SetSetting(PropertyID.Tv, 0x5C);
                    break;
                case "1/25":
                    MainCamera.SetSetting(PropertyID.Tv, 0x5D);
                    break;
                case "1/30":
                    MainCamera.SetSetting(PropertyID.Tv, 0x60);
                    break;
                case "1/40":
                    MainCamera.SetSetting(PropertyID.Tv, 0x63);
                    break;
                case "1/45":
                    MainCamera.SetSetting(PropertyID.Tv, 0x64);
                    break;
                case "1/50":
                    MainCamera.SetSetting(PropertyID.Tv, 0x65);
                    break;
                case "1/60":
                    MainCamera.SetSetting(PropertyID.Tv, 0x68);
                    break;
                case "1/80":
                    MainCamera.SetSetting(PropertyID.Tv, 0x6B);
                    break;
                case "1/90":
                    MainCamera.SetSetting(PropertyID.Tv, 0x6C);
                    break;
                case "1/100":
                    MainCamera.SetSetting(PropertyID.Tv, 0x6D);
                    break;
                case "1/125":
                    MainCamera.SetSetting(PropertyID.Tv, 0x70);
                    break;
                case "1/160":
                    MainCamera.SetSetting(PropertyID.Tv, 0x73);
                    break;
                case "1/180":
                    MainCamera.SetSetting(PropertyID.Tv, 0x74);
                    break;
                case "1/200":
                    MainCamera.SetSetting(PropertyID.Tv, 0x75);
                    break;
                case "1/250":
                    MainCamera.SetSetting(PropertyID.Tv, 0x78);
                    break;
                case "1/320":
                    MainCamera.SetSetting(PropertyID.Tv, 0x7B);
                    break;
                case "1/350":
                    MainCamera.SetSetting(PropertyID.Tv, 0x7C);
                    break;
                case "1/400":
                    MainCamera.SetSetting(PropertyID.Tv, 0x7D);
                    break;
                case "1/500":
                    MainCamera.SetSetting(PropertyID.Tv, 0x80);
                    break;
                case "1/640":
                    MainCamera.SetSetting(PropertyID.Tv, 0x83);
                    break;
                case "1/750":
                    MainCamera.SetSetting(PropertyID.Tv, 0x84);
                    break;
                case "1/800":
                    MainCamera.SetSetting(PropertyID.Tv, 0x85);
                    break;
                case "1/1000":
                    MainCamera.SetSetting(PropertyID.Tv, 0x88);
                    break;
                case "1/1250":
                    MainCamera.SetSetting(PropertyID.Tv, 0x8B);
                    break;
                case "1/1500":
                    MainCamera.SetSetting(PropertyID.Tv, 0x8C);
                    break;
                case "1/1600":
                    MainCamera.SetSetting(PropertyID.Tv, 0x8D);
                    break;
                case "1/2000":
                    MainCamera.SetSetting(PropertyID.Tv, 0x90);
                    break;
                case "1/2500":
                    MainCamera.SetSetting(PropertyID.Tv, 0x93);
                    break;
                case "1/3000":
                    MainCamera.SetSetting(PropertyID.Tv, 0x94);
                    break;
                case "1/3200":
                    MainCamera.SetSetting(PropertyID.Tv, 0x95);
                    break;
                case "1/4000":
                    MainCamera.SetSetting(PropertyID.Tv, 0x98);
                    break;
                case "1/5000":
                    MainCamera.SetSetting(PropertyID.Tv, 0x9B);
                    break;
                case "1/6000":
                    MainCamera.SetSetting(PropertyID.Tv, 0x9C);
                    break;
                case "1/6400":
                    MainCamera.SetSetting(PropertyID.Tv, 0x9D);
                    break;
                case "1/8000":
                    MainCamera.SetSetting(PropertyID.Tv, 0xA0);
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// Sets iso settings
        /// </summary>
        /// <param name="value"></param>
        public void SetISO(string setting)
        {
            switch (setting)
            {
                case "ISO Auto":
                    MainCamera.SetSetting(PropertyID.ISO, 0);
                    break;
                case "ISO 50":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000040);
                    break;
                case "ISO 100":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000048);
                    break;
                case "ISO 125":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000004b);
                    break;
                case "ISO 160":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000004d);
                    break;
                case "ISO 200":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000050);
                    break;
                case "ISO 250":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000053);
                    break;
                case "ISO 320":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000055);
                    break;
                case "ISO 400":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000058);
                    break;
                case "ISO 500":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000005b);
                    break;
                case "ISO 640":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000005d);
                    break;
                case "ISO 800":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000060);
                    break;
                case "ISO 1000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000063);
                    break;
                case "ISO 1250":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000065);
                    break;
                case "ISO 1600":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000068);
                    break;
                case "ISO 2000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000006b);
                    break;
                case "ISO 2500":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000006d);
                    break;
                case "ISO 3200":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000070);
                    break;
                case "ISO 4000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000073);
                    break;
                case "ISO 5000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000075);
                    break;
                case "ISO 6400":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000078);
                    break;
                case "ISO 8000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000007b);
                    break;
                case "ISO 10000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x0000007d);
                    break;
                case "ISO 12800":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000080);
                    break;
                case "ISO 16000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000083);
                    break;
                case "ISO 20000":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000085);
                    break;
                case "ISO 25600":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000088);
                    break;
                case "ISO 51200":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000090);
                    break;
                case "ISO 102400":
                    MainCamera.SetSetting(PropertyID.ISO, 0x00000098);
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// Sets white balance settings
        /// </summary>
        /// <param name="value"></param>
        private void SetWhiteBalance(string setting)
        {
            switch (setting)
            {
                case "Pasted":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, -2);
                    break;
                case "Click":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, -1);
                    break;
                case "Auto":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 0);
                    break;
                case "Daylight":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 1);
                    break;
                case "Cloudy":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 2);
                    break;
                case "Tungsten":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 3);
                    break;
                case "Fluorescent":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 4);
                    break;
                case "Strobe":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 5);
                    break;
                case "WhitePaper":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 6);
                    break;
                case "Shade":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 8);
                    break;
                case "ColorTemperature":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 9);
                    break;
                case "PCSet1":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 10);
                    break;
                case "PCSet2":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 11);
                    break;
                case "PCSet3":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 12);
                    break;
                case "WhitePaper2":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 15);
                    break;
                case "WhitePaper3":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 16);
                    break;
                case "WhitePaper4":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 18);
                    break;
                case "WhitePaper5":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 19);
                    break;
                case "PCSet4":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 20);
                    break;
                case "PCSet5":
                    MainCamera.SetSetting(PropertyID.WhiteBalance, 21);
                    break;
                default:
                    break;
            }
        }


        #endregion



        #region Camera Profiles
        // ================================================================== CAMERA PROFILES ================================================================== //


        const string CAMERACONFIGFILE = @"camera_profiles.json";    // the filename of the camera configuration file
        List<CameraProfile> CameraProfileList;                      // a list of CameraProfiles


        /// <summary>
        /// Retrieves the list of camera profiles and loads them into the program
        /// </summary>
        private void LoadCameraProfiles()
        {
            using (StreamReader r = new StreamReader(CAMERACONFIGFILE))
            {
                string json = r.ReadToEnd();
                CameraProfileList = JsonConvert.DeserializeObject<List<CameraProfile>>(json);

                // add each camera profile to the camera profiles listview
                foreach (CameraProfile profile in CameraProfileList)
                {
                    // Adds the profile to the CameraProfilesListView
                    // the profile name
                    Label proName = new Label()
                    {
                        Content = "[" + profile.camera + "] " + profile.name,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 10,
                    };

                    // the profile descriptors
                    TextBlock proDesc = new TextBlock()
                    {
                        Text = "F-Stop: " + profile.fstop + ", Exposure: " + profile.exposure + ", ISO: " + profile.iso + ", WhiteBalance: " + profile.whiteBalance,
                        FontStyle = FontStyles.Italic,
                        TextWrapping = TextWrapping.WrapWithOverflow,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 10,
                    };

                    // creates the StackPanel to hold all the content
                    StackPanel item = new StackPanel()
                    {
                        Height = 50,
                    };
                    item.Children.Add(proName);
                    item.Children.Add(proDesc);

                    // adds the collection StackPanel to the CollectionListView
                    CameraProfilesListView.Items.Add(item);

                }
            }
        }


        /// <summary>
        /// an object to store the camera profile details
        /// </summary>
        class CameraProfile
        { 
            public string name { get; set; }            // the camera profile name
            public string camera { get; set; }          // the camera used to make this profile
            public string fstop { get; set; }           // the F-stop setting
            public string exposure { get; set; }        // the exposure (shutter-speed) setting
            public string iso { get; set; }             // the ISO setting
            public string whiteBalance { get; set; }    // the white balance setting


            /// <summary>
            /// Default constructor for a camera profile
            /// </summary>
            public CameraProfile()
            {
                name = null;
                camera = null;
                fstop = null;
                exposure = null;
                iso = null;
                whiteBalance = null;
            }


            /// <summary>
            /// Constructor for a camera profile
            /// </summary>
            /// <param name="nameVal">the camera profile name</param>
            /// <param name="camVal">the camera name</param>
            /// <param name="fstopVal">the F-stop setting</param>
            /// <param name="expoVal">the exposure (shutter-speed) setting</param>
            /// <param name="isoVal">the ISO setting</param>
            /// <param name="wbVal">the white balance setting</param>
            public CameraProfile(string nameVal, string camVal, string fstopVal, string expoVal, string isoVal, string wbVal)
            {
                name = nameVal;
                camera = camVal;
                fstop = fstopVal;
                exposure = expoVal;
                iso = isoVal;
                whiteBalance = wbVal;
            }

        }


        #endregion



        #region Collection Profiles
        // ================================================================== COLLECTION PROFILES ================================================================== //

        
        const string COLLECTIONCONFIGFILE = @"collection_profiles.json";    // the filename of the collection configuration file
        List<CollectionProfile> CollectionProfileList;                      // a list of CollectionProfiles


        /// <summary>
        /// Retrieves the list of collection profiles and loads them into the program
        /// </summary>
        private void LoadCollectionProfiles()
        {
            using (StreamReader r = new StreamReader(COLLECTIONCONFIGFILE))
            {
                string json = r.ReadToEnd();
                CollectionProfileList = JsonConvert.DeserializeObject<List<CollectionProfile>>(json);

                // add each camera profile to the camera profiles combobox
                foreach (CollectionProfile collection in CollectionProfileList)
                {
                    CollectionComboBox.Items.Add(collection.name);

                    // Adds the collection to the CollectionListView
                    // the collection name
                    Label colName = new Label()
                    {
                        Content = collection.name,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                    };

                    // the collection descriptors
                    Label colDesc = new Label()
                    {
                        Content = "Col #: " + collection.collectionNumber + ", Poses: " + collection.numberOfPoses,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 10,
                    };

                    // the collection savepath
                    Label colSavePath = new Label()
                    {
                        Content = "Save path: " + collection.savingDirectory,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 10,
                    };

                    // creates the StackPanel to hold all the content
                    StackPanel item = new StackPanel()
                    {
                        Height = 70,
                    };
                    item.Children.Add(colName);
                    item.Children.Add(colDesc);
                    item.Children.Add(colSavePath);

                    // adds the collection StackPanel to the CollectionListView
                    CollectionListView.Items.Add(item);

                }
            }
        }


        /// <summary>
        /// Details of the collection
        /// </summary>
        class CollectionProfile
        {
            public string name { get; set; }                // the collection profile name
            public string collectionNumber { get; set; }    // the collection number
            public int numberOfPoses { get; set; }          // the number of poses in the collection
            public string savingDirectory { get; set; }     // the save path of the collection
            public List<Pose> poses { get; set; }           // the list of camera profiles that make up the collection


            /// <summary>
            /// Default constructor for a collection profile
            /// </summary>
            public CollectionProfile()
            {
                name = null;
                collectionNumber = null;
                numberOfPoses = 0;
                savingDirectory = null;
                poses = null;
            }


            /// <summary>
            /// Constructor for a collection profile
            /// </summary>
            /// <param name="nameVal">the collection profile name</param>
            /// <param name="colNum">the collection number</param>
            /// <param name="nrPoses">the number of poses in the collection</param>
            /// <param name="savePath">the save path of the collection</param>
            public CollectionProfile(string nameVal, string colNum, int nrPoses, string savePath)
            {
                name = nameVal;
                collectionNumber = colNum;
                numberOfPoses = nrPoses;
                savingDirectory = savePath;
                poses = new List<Pose>();
            }


            /// <summary>
            /// Adds a pose to the collection
            /// </summary>
            /// <param name="pose"></param>
            public void Add(Pose pose)
            {
                poses.Add(pose);
            }
        }


        /// <summary>
        /// Details of a pose
        /// </summary>
        class Pose
        {
            public string title { get; set; }                  // the pose title
            public string description { get; set; }            // the description of the pose
            public string thumbnail { get; set; }              // the thumbnail for the pose
            public string device { get; set; }                 // the device name for the pose
            public string filename { get; set; }               // the filename for the pose
            CameraProfile cameraProfile { get; set; }   // the camera profile for the pose


            /// <summary>
            /// Default constructor for a pose
            /// </summary>
            public Pose()
            {
                title = null;
                description = null;
                thumbnail = null;
                device = null;
                filename = null;
                cameraProfile = null;
            }

            
            /// <summary>
            /// Constructor for a pose
            /// </summary>
            /// <param name="titleVal">the pose title</param>
            /// <param name="descVal">the description of the pose</param>
            /// <param name="thumbSource">the thumbnail for the pose</param>
            /// <param name="devVal">the device name for the pose</param>
            /// <param name="filnamVal">the filename for the pose</param>
            /// <param name="camProfile">the camera profile for the pose</param>
            public Pose(string titleVal, string descVal, string thumbSource, string devVal, string filnamVal, CameraProfile camProfile)
            {
                title = titleVal;
                description = descVal;
                thumbnail = thumbSource;
                device = devVal;
                filename = filnamVal;
                cameraProfile = camProfile;
            }
        }


        #endregion



        #region UI Functions
        // ================================================================== UI FUNCTIONS ================================================================== //


        private Boolean isCameraChanged = false;    // boolean to check if camera settings have changed
                                                    // to know when to reapply original camera settings               
        
        
        /// <summary>
        /// Switches between window screens
        /// </summary>
        /// <param name="screen"></param>
        private void SwitchScreen(string screen)
        {

            var bc = new BrushConverter();
            if (screen == "home")
            {
                // TODO: reapply original camera settings if previously on CameraScreen
                //if (isCameraChanged == true && )
                //{
                //    resetLiveView();
                //    isCameraChanged = false;
                //}

                // switch screens
                HomeScreenGrid.Visibility = Visibility.Visible;
                CameraScreenGrid.Visibility = Visibility.Collapsed;
                CollectionsScreenGrid.Visibility = Visibility.Collapsed;

                // button highlighting
                TopToolBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#2C2A29");
                HomeNavigationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#FF3F4A52");
                CameraNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
                CollectionsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
            }
            else if (screen == "camera")
            {
                // switch screens
                HomeScreenGrid.Visibility = Visibility.Collapsed;
                CameraScreenGrid.Visibility = Visibility.Visible;
                CollectionsScreenGrid.Visibility = Visibility.Collapsed;

                // button highlighting
                TopToolBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#802115");
                HomeNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
                CameraNavigationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#AA4639");
                CollectionsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
            }
            else if (screen == "collections")
            {
                // TODO: reapply original camera settings if previously on CameraScreen
                //if (isCameraChanged == true)
                //{
                //    resetLiveView();
                //    isCameraChanged = false;
                //}

                // switch screens
                HomeScreenGrid.Visibility = Visibility.Collapsed;
                CameraScreenGrid.Visibility = Visibility.Collapsed;
                CollectionsScreenGrid.Visibility = Visibility.Visible;

                // button highlighting
                TopToolBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#00386F");
                HomeNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
                CameraNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
                CollectionsNavigationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#00498F");
            }
            else
                ShowMessage("red", "Ummm...", "If you see this it means that I mispelled one of the navigation screens while I was coding...Sorry....");

        }


        /// <summary>
        /// Reveals a customizable message box
        /// </summary>
        /// <param name="color"></param>
        /// <param name="title"></param>
        /// <param name="message"></param>
        private void ShowMessage(string color, string title, string message)
        {
            this.Dispatcher.Invoke(() =>
            {
                // reveals the message grid
                MessageGrid.Visibility = Visibility.Visible;

                // converts the color of the message bar
                var bc = new BrushConverter();
                if (color == "red")
                {
                    MessageBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#BE3A34");
                    MessageBarLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                }
                else if (color == "yellow")
                {
                    MessageBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#FDDA24");
                    MessageBarLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#000000");
                }
                else if (color == "green")
                {
                    MessageBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#9ABEAA");
                    MessageBarLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#000000");
                }

                // fills in the title and the message content
                MessageBarLabel.Content = title;
                if (message.Length > 50)
                {
                    MessageLabel1.Content = message.Substring(0, 50);
                    MessageLabel2.Content = message.Substring(50, message.Length - 50);
                }
                else
                {
                    MessageLabel1.Content = message;
                }
            });
        }


        /// <summary>
        /// Hides any visible error or warning message
        /// </summary>
        private void HideMessage()
        {
            MessageGrid.Visibility = Visibility.Collapsed;
        }


        /// <summary>
        /// Make it look fabulous
        /// </summary>
        private void EasterEgg()
        {
            ShowMessage("green", "SYIHAN IS KWEEN", "Syihan is Queen");
        }


        #endregion


        
        #region Session Functionality
        // ================================================================== SESSION FUNCTIONALITY ================================================================== //

        private int CaptureNumber;              // int which stores the index of the current capture in the collection
        private const int HIGHRESOLUTION = 375; // the height of a high resolution thumbnail
        private const int LOWRESOLUTION = 183;  // the height of a low resolution thumbnail


        /// <summary>
        /// Begins the session
        /// </summary>
        private void BeginSession()
        {
            // TODO: toggle proper UI buttons
            // TODO: assign variables
        }


        /// <summary>
        /// Takes a picture of the subject during a session
        /// </summary>
        /// <param name="notRecapture">boolean that is true if the capture is a regular capture, false if it is a recapture</param>
        private void CaptureSubject(Boolean notRecapture)
        {
            // TODO: take picture
            TakePicture();
        }
        
        
        /// <summary>
        /// Saves the file to the specified filepath
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="filename"></param>
        private void SaveFile(string dir, string filename)
        {
            string filepath = dir + "\\" + filename;
            // TODO: implement saving to filepath
        }


        /// <summary>
        /// Populates the PoseListView with collection thumbnails
        /// </summary>
        private void PopulatePoseListView()
        {
            CollectionProfile collection = CollectionProfileList.ElementAt(CollectionComboBox.SelectedIndex);
            List<Pose> poses = collection.poses;

            //title = null;
            //description = null;
            //thumbnail = null;
            //device = null;
            //filename = null;
            //cameraProfile = null;

            foreach (Pose pose in poses)
            {

                // Adds the pose to the PoseListView
                // the pose name
                Label poseTitle = new Label()
                {
                    Content = pose.title,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                    FontSize = 12,
                };

                // the pose description
                Label poseDesc = new Label()
                {
                    Content = pose.description,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                    FontSize = 12,
                };

                // creates the StackPanel to hold all the content
                StackPanel item = new StackPanel()
                {
                    Width = 100,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                item.Children.Add(poseTitle);
                item.Children.Add(poseDesc);

                // adds the collection StackPanel to the CollectionListView
                PoseListView.Items.Add(item);
            }

            //string thumbnailSource = null;
            //LoadImage(thumbnailSource, LOWRESOLUTION);
        }


        /// <summary>
        /// Updates the pose thumbnail specified
        /// </summary>
        /// <param name="index">the index of the thumbnail that must be updated</param>
        private void UpdatePoseThumbnail(int index)
        {
            // TODO: IMPLEMENT THE CODE TO UPDATE A THUMBNAIL
        }


        /// <summary>
        /// Loads a BitmapImage
        /// </summary>
        /// <param name="source">the filepath of the image to be loaded</param>
        /// <param name="imageHeight">the height resolution of the image</param>
        private BitmapImage LoadImage(string source, int imageHeight)
        {
            // TODO: COPY LOADING BITMAP IMAGE CODE
            return new BitmapImage();
        }



        #endregion



        #region Miscellaneous Event Handlers
        // ================================================================== MISCELLANEOUS EVENT HANDLERS ================================================================== //

        
        /// <summary>
        /// Event handler to navigate to the Home Screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HomeNavigationButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchScreen("home");
        }


        /// <summary>
        /// Event handler to navigate to the Camera Screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraNavigationButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchScreen("camera");
        }


        /// <summary>
        /// Event handler to navigate to the Collections Screen
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionsNavigationButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchScreen("collections");
        }


        /// <summary>
        /// Event handler to reveal About page
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AboutProgramMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageGrid.Visibility = Visibility.Visible;
            AboutProgramBox.Visibility = Visibility.Visible;
        }


        /// <summary>
        /// Event handler for non-severe errors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ex"></param>
        private void ErrorHandler_NonSevereErrorHappened(object sender, ErrorCode ex)
        {
            ShowMessage("red", "ERROR", $"SDK Error code: {ex} ({((int)ex).ToString("X")})");
        }


        /// <summary>
        /// Event handler for severe errors
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ex"></param>
        private void ErrorHandler_SevereErrorHappened(object sender, Exception ex)
        {
            ShowMessage("red", "ERROR", ex.Message);
        }


        /// <summary>
        /// Event handler for when the program is closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MainCamera?.CloseSession();
            MainCamera?.Dispose();
            API?.Dispose();
        }


        #endregion


        
        #region Home Screen Event Handlers
        // ================================================================== HOME SCREEN EVENT HANDLERS ================================================================== //


        /// <summary>
        /// Toggle Live View button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleLiveViewHomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MainCamera.SessionOpen)
                SetLiveViewOn(true);
            else
                SetLiveViewOn(false);
        }


        /// <summary>
        /// Changing the selection within the Camera ComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CameraComboBox.SelectedIndex != 0)
            {
                ToggleLiveViewHomeButton.Visibility = Visibility.Visible;
                SetLiveViewOn(true);
                InitializeCameraSettings();

                // selects the current settings
                FStopComboBox.SelectedIndex = FStopComboBox.Items.IndexOf(AvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Av)).StringValue);
                ExposureComboBox.SelectedIndex = ExposureComboBox.Items.IndexOf(TvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Tv)).StringValue);
                ISOComboBox.SelectedIndex = ISOComboBox.Items.IndexOf(ISOValues.GetValue(MainCamera.GetInt32Setting(PropertyID.ISO)).StringValue);
                WhiteBalanceComboBox.SelectedIndex = WhiteBalanceComboBox.Items.IndexOf(MainCamera.GetStringSetting(PropertyID.WhiteBalance));

                // enables all of the camera settings comboboxes on the camera screen
                FStopComboBox.IsEnabled = true;
                ExposureComboBox.IsEnabled = true;
                ISOComboBox.IsEnabled = true;
                WhiteBalanceComboBox.IsEnabled = true;
                CameraSettings_ApplyChanges.IsEnabled = true;
                CameraProfileNameTextBox.IsEnabled = true;
                SaveCameraProfileButton.IsEnabled = true;
            }
            else
            {
                if(ToggleLiveViewHomeButton != null)
                    ToggleLiveViewHomeButton.Visibility = Visibility.Collapsed;
                SetLiveViewOn(false);

                // disables all of the camera settings comboboxes on the camera screen
                if (FStopComboBox != null)
                    FStopComboBox.IsEnabled = false;
                if (ExposureComboBox != null)
                    ExposureComboBox.IsEnabled = false;
                if (ISOComboBox != null)
                    ISOComboBox.IsEnabled = false;
                if (WhiteBalanceComboBox != null)
                    WhiteBalanceComboBox.IsEnabled = false;
                if (CameraSettings_ApplyChanges != null)
                    CameraSettings_ApplyChanges.IsEnabled = false;
                if (CameraProfileNameTextBox != null)
                    CameraProfileNameTextBox.IsEnabled = false;
                if (SaveCameraProfileButton != null)
                    SaveCameraProfileButton.IsEnabled = false;
            }
        }


        /// <summary>
        /// Button that hides the error on the Error Grid
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideMessageButton_Click(object sender, RoutedEventArgs e)
        {
            HideMessage();
            AboutProgramBox.Visibility = Visibility.Collapsed;
        }


        /// <summary>
        /// Event handler that begins a new session
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EnterRIDButton_Click(object sender, RoutedEventArgs e)
        {

            // converts the RID input to lowercase and splits it with "_"
            string input = RIDTextBox.Text.ToLower();
            string[] ridDateCol = input.Split('_');
            string rid;
            string date;
            string col;

            // funny easter egg
            if (input.Contains("queen"))
                EasterEgg();

            // throws an error if none of the session settings fields are filled
            else if (CameraComboBox.Text.Length == 0 || CollectionComboBox.Text.Length == 0 || RIDTextBox.Text.Length == 0)
                ShowMessage("red", "Unfilled variables", "Must fill out all fields before beginning session.");

            // verifies that the RID, date, and collection number are all in the right format
            else if (ridDateCol.Length == 3)
            {
                // assigns the session attributes
                rid = ridDateCol[0];
                date = ridDateCol[1];
                col = ridDateCol[2];

                // TODO: GET RID OF THIS TEST BIT OF CODE
                ShowMessage("green", "Values attributed", "RID: " + rid + ", Date: " + date + ", Collection Number: " + col);

                if (rid.Length != 7 && date.Length != 8 && (col.Length != 1 || col.Length != 2))
                    ShowMessage("red", "Invalid input", "RID_Date_Col must be in the following format: <7 DIGITS>_<8 DIGITS>_<1-2 DIGITS>");
            }
            else
                ShowMessage("red", "Invalid input", "Input must be in the following form: <RID>_<DATE>_<COLLECTION NUMBER>");

        }


        /// <summary>
        /// Event handler that loads a new collection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Collection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PoseListView.Items.Clear();
            PopulatePoseListView();
        }





        #endregion

        #region Camera Screen Event Handlers
        // ================================================================== CAMERA SCREEN EVENT HANDLERS ================================================================== //

        /// <summary>
        /// Deselects selected profile when pressing the FStopComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FStopComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfilesListView.SelectedItem = null;
        }


        /// <summary>
        /// Deselects selected profile when pressing the ExposureComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExposureComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfilesListView.SelectedItem = null;
        }


        /// <summary>
        /// Deselects selected profile when pressing the ISOComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ISOComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfilesListView.SelectedItem = null;
        }


        /// <summary>
        /// Deselects selected profile when pressing the WhiteBalanceComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WhiteBalanceComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfilesListView.SelectedItem = null;
        }


        /// <summary>
        /// Applies the changes to the live view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraSettings_ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            CameraProfilesListView.SelectedItem = null;

            // if all required fields are filled (f-stop, exposure, iso), changes are applied
            if(FStopComboBox.SelectedItem != null &&
                ExposureComboBox.SelectedItem != null &&
                ISOComboBox.SelectedItem != null )
            {
                // applies changes
                SetFStop(FStopComboBox.SelectedItem.ToString());
                SetExposure(ExposureComboBox.SelectedItem.ToString());
                SetISO(ISOComboBox.SelectedItem.ToString());
                if (WhiteBalanceComboBox.SelectedItem != null)
                    SetWhiteBalance(WhiteBalanceComboBox.SelectedItem.ToString());

                // turns off live view and then turns it back on
                SetLiveViewOn(false);
                System.Threading.Thread.Sleep(250);
                SetLiveViewOn(true);
            }
            // if required fields are not filled, throws an error
            else
            {
                ShowMessage("red", "Unfilled fields", "F-Stop/Aperture, Exposure/Shutter Speed, and ISO must be filled before applying changes.");
            }
        }


        /// <summary>
        /// Updates camera profile settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraProfilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // if a camera is connected, load settings
            if (CameraComboBox.SelectedIndex != 0 && CameraProfilesListView.SelectedItem != null)
            {
                // if the profile camera matches the connected device name, load settings
                if (CameraProfileList.ElementAt(CameraProfilesListView.SelectedIndex).camera == MainCamera.DeviceName)
                {
                    DeleteCameraProfileButton.IsEnabled = true;

                    // retrieves the information from the camera profile config file
                    CameraProfile profile = CameraProfileList.ElementAt(CameraProfilesListView.SelectedIndex);

                    // inserts camera profile information
                    FStopComboBox.SelectedItem = profile.fstop;
                    ExposureComboBox.SelectedItem = profile.exposure;
                    ISOComboBox.SelectedItem = profile.iso;
                    WhiteBalanceComboBox.SelectedItem = profile.whiteBalance;
                }
                // if the camera connected does not match the device name, throw an error
                else
                {
                    ShowMessage("red", "Specified camera not connected.", "Only camera profiles registered with the connected camera can be loaded.");
                    // TODO: Find a way to deselect the index without throwing off an OutOfBounds error in the CameraProfilesListView
                    //CameraProfilesListView.SelectedItem = null;
                }
            }
            // if a camera is not connected, throw an error
            else if (CameraProfilesListView.SelectedItem != null)
            {
                ShowMessage("red", "No camera selected", "Camera must be connected to load camera settings.");
                CameraProfilesListView.SelectedItem = null;
            }
            // disables the delete button if a profile is not selected
            else
                DeleteCameraProfileButton.IsEnabled = false;
        }


        /// <summary>
        /// Deletes the selected camera profile
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteCameraProfileButton_Click(object sender, RoutedEventArgs e)
        {

        }


        #endregion

        #region Collection Screen Event Handlers
        // ================================================================== COLLECTION SCREEN EVENT HANDLERS ================================================================== //


        /// <summary>
        /// Updates collection settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollectionListView.SelectedItem == null)
            {
                DeleteCollectionButton.IsEnabled = false;
            }
            else
            {
                CollectionProfile collection = CollectionProfileList.ElementAt(CollectionListView.SelectedIndex);

                DeleteCollectionButton.IsEnabled = true;
                SaveDirectoryTextBox.Text = collection.savingDirectory;
                NrPosesTextBox.Text = collection.numberOfPoses.ToString();
                CollectionNrTextBox.Text = collection.collectionNumber;
            }
        }


        /// <summary>
        /// Opens folder browser selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrowseSaveDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: open folder browser selection
        }


        /// <summary>
        /// Deselects selected profile when the text is changed in SaveDirectoryTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveDirectoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Deselects selected profile when a key is pressed in NrPosesTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NrPosesTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Deselects selected profile when a space or backspace is pressed in NrPosesTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NrPosesTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Space || e.Key == Key.Back)
                CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Deselects selected profile when a key is pressed in NrPosesTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionNrTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Deselects selected profile when a space or backspace is pressed in NrPosesTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionNrTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Back)
                CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Applies changes to collection settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionSettings_ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            // deselects item on CollectionListView
            CollectionListView.SelectedItem = null;

            // verifies that all fields have been filled
            if (SaveDirectoryTextBox.Text.Length <= 0 || NrPosesTextBox.Text.Length <= 0 || CollectionNrTextBox.Text.Length <= 0)
            {
                ShowMessage("red", "Error", "All fields must be filled.");
                return;
            }

            // checks if the values entered are integers
            int nrPoses;
            int collectionNr;
            if (!int.TryParse(NrPosesTextBox.Text, out nrPoses))
            {
                ShowMessage("red", "Error", "\"# of Poses\" accepts only integers.");
                return;
            }
            if (!int.TryParse(CollectionNrTextBox.Text, out collectionNr))
            {
                ShowMessage("red", "Error", "\"Collection #\" accepts only integers.");
                return;
            }
        }



        #endregion
    }
}

using EDSDKLib;
using EOSDigital.API;
using EOSDigital.SDK;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


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
            DisplaySplashScreen();
            InitializeComponent();

            // initializes the API and retrieves the list of connected cameras
            API = new CanonAPI();
            LoadCameraList();
            LoadCameraProfiles();
            LoadCollections();

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
            Thread.Sleep(3000);  // TODO: MAKE SPLASH SCREEN LAST LONGER

        }


        #endregion



        #region Camera and Live View
        // ================================================================== CAMERA AND LIVE VIEW ================================================================== //


        public CanonAPI API;                                    // the class that mainly handles the SDK lifetime, the connected Cameras and the SDK events
        public Camera MainCamera;                               // the main camera
        private List<Camera> CameraList;                        // the list of cameras available for connection
        private ImageBrush LiveViewBrush = new ImageBrush();    // the brush to paint the Live View canvas
        private Action<BitmapImage> SetImageAction;             // the accumulation of Live View images
        private bool IsCameraOn;                                // boolean to determine if the camera is on


        /// <summary>
        /// Initializes the camera list
        /// </summary>
        private void LoadCameraList()
        {
            // clears list of cameras
            this.Dispatcher.Invoke(() =>
            {
                for (int i = CameraComboBox.Items.Count - 1; i > 0; i--)
                    CameraComboBox.Items.RemoveAt(i);
            });

            // retrieves all Canon cameras connected to the computer
            API.CameraAdded += API_CameraAdded;
            CameraList = API.GetCameraList();

            // populates the Camera ComboBox with the list
            foreach (Camera cameraOption in CameraList)
                this.Dispatcher.Invoke(() => { CameraComboBox.Items.Add(cameraOption.DeviceName); });
        }


        /// <summary>
        /// Toggles the live view on/off
        /// </summary>
        private void SetLiveViewOn(Boolean on)
        {
            // if ON is true, resume live view
            if (on)
            {
                // closes any already open camera session
                MainCamera?.CloseSession();

                // selects the camera chosen in the Camera ComboBox and opens a new session
                MainCamera = CameraList[CameraComboBox.SelectedIndex - 1];

                // opens a brand new camera session
                MainCamera.OpenSession();
                IsCameraOn = true;

                // sets up the LiveViewCanvas
                SetImageAction = (BitmapImage img) => { LiveViewBrush.ImageSource = img; };

                // paints the LiveViewCanvas
                if (HomeScreenGrid.Visibility == Visibility.Visible)
                {
                    HomeLiveViewCanvas.Background = LiveViewBrush;
                    CameraLiveViewCanvas.Background = System.Windows.Media.Brushes.Black;
                }
                if (CameraScreenGrid.Visibility == Visibility.Visible)
                {
                    HomeLiveViewCanvas.Background = System.Windows.Media.Brushes.Transparent;
                    CameraLiveViewCanvas.Background = LiveViewBrush;
                }

                // starts the live view
                MainCamera.StartLiveView();

                // sets up camera event handlers
                MainCamera.LiveViewUpdated += MainCamera_LiveViewUpdated;
                //MainCamera.ProgressChanged += MainCamera_ProgressChanged;
                MainCamera.StateChanged += MainCamera_StateChanged;
                MainCamera.DownloadReady += MainCamera_DownloadReady;

                // adjusts some UI elements
                ToggleLiveViewHomeButton.Content = "\u23f8";
                CaptureButton.IsEnabled = true;
                CameraControlsAutofocusButton.IsEnabled = true;
                // the Camera Screen current settings
                FStopLabel.Content = AvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Av)).StringValue;
                ExposureLabel.Content = TvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Tv)).StringValue;
                ISOLabel.Content = ISOValues.GetValue(MainCamera.GetInt32Setting(PropertyID.ISO)).StringValue;
                // the Camera Screen profile settings
                FStopComboBox.IsEnabled = true;
                ExposureComboBox.IsEnabled = true;
                ISOComboBox.IsEnabled = true;
                WhiteBalanceComboBox.IsEnabled = true;
                CameraScreenAutofocusButton.Visibility = Visibility.Visible;
                if (!IsSessionOngoing)
                {
                    CameraSettings_ApplyChangesButton.IsEnabled = true;
                    CameraProfileNameTextBox.IsEnabled = true;
                    SaveCameraProfileButton.IsEnabled = true;
                }
            }

            // if ON is false, close the camera session
            else
            {
                try
                {
                    // closes the camera session
                    MainCamera?.StopLiveView();
                    MainCamera?.CloseSession();
                    MainCamera.DownloadReady -= MainCamera_DownloadReady;
                    IsCameraOn = false;

                    // paints the LiveView black
                    HomeLiveViewCanvas.Background = System.Windows.Media.Brushes.Transparent;
                    CameraLiveViewCanvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));

                    // adjusts some UI elements
                    ToggleLiveViewHomeButton.Content = "\u25b6";
                    SessionSettingsAutofocusButton.Visibility = Visibility.Hidden;
                    CaptureButton.IsEnabled = false;
                    CameraControlsAutofocusButton.IsEnabled = false;
                    // the Camera Screen current settings
                    FStopLabel.Content = "N/A";
                    ExposureLabel.Content = "N/A";
                    ISOLabel.Content = "N/A";
                    // the Camera Screen profile settings
                    FStopComboBox.IsEnabled = false;
                    ExposureComboBox.IsEnabled = false;
                    ISOComboBox.IsEnabled = false;
                    WhiteBalanceComboBox.IsEnabled = false;
                    CameraSettings_ApplyChangesButton.IsEnabled = false;
                    CameraProfileNameTextBox.IsEnabled = false;
                    SaveCameraProfileButton.IsEnabled = false;
                    CameraScreenAutofocusButton.Visibility = Visibility.Hidden;
                }
                catch { }
            }
        }


        /// <summary>
        /// Focuses the camera
        /// </summary>
        private void FocusCamera()
        {
            try { MainCamera.SendCommand(CameraCommand.DoEvfAf, (int)EDSDK.EdsEvfAf.CameraCommand_EvfAf_ON); }
            catch (Exception ex) { ShowMessage("red", "Error", ex.Message + "\nPlease try toggling the Live View on and off."); }
        }


        /// <summary>
        /// Unfocuses the camera
        /// </summary>
        private void UnfocusCamera()
        {
            try { MainCamera.SendCommand(CameraCommand.DoEvfAf, (int)EDSDK.EdsEvfAf.CameraCommand_EvfAf_OFF); }
            catch (Exception ex) { ShowMessage("red", "Error", ex.Message + "\nPlease try toggling the Live View on and off."); }
        }


        /// <summary>
        /// Takes a picture
        /// </summary>
        private void TakePicture()
        {
            /* 
             * WARNING: 
             * THE FOLLOWING CODE IS EXTREMELY SKETCHY BECAUSE I HAD TO CHANGE A METHOD IN THE CANON SDK.
             * Changed method "private ErrorCode Camera_SDKObjectEvent(ObjectEventID inEvent, IntPtr inRef, IntPtr inContext)"
             * in Camera.cs (Lines 350-359). Original code is still there just in case.
             * 
             * The above method is triggered once a captured image is ready to be downloaded.
             */
            try {
                // takes the picture
                MainCamera.SendCommand(CameraCommand.PressShutterButton, (int)ShutterButton.Completely_NonAF);

                // locks the UI elements to avoid overloading the camera
                CaptureButton.IsEnabled = false;
                RecaptureButton.Visibility = Visibility.Hidden;
                SessionSettingsAutofocusButton.IsEnabled = false;
                RecaptureAutofocusButton.Visibility = Visibility.Hidden;
                CameraControlsAutofocusButton.IsEnabled = false;
                CameraScreenAutofocusButton.IsEnabled = false;
                ToggleLiveViewHomeButton.Visibility = Visibility.Hidden;
                CloseSessionButton.IsEnabled = false;
            }
            catch (Exception ex) { ShowMessage("red", "Error", ex.Message + "\nPlease try toggling the Live View on and off."); }
        }


        /// <summary>
        /// Sets the camera settings 
        /// </summary>
        /// <param name="fstop"></param>
        /// <param name="exposure"></param>
        /// <param name="iso"></param>
        /// <param name="wb"></param>
        private void SetCameraSettings(string fstop = null, string exposure = null, string iso = null, string wb = "Auto")
        {
            // applies changes
            SetFStop(fstop);
            SetExposure(exposure);
            SetISO(iso);
            SetWhiteBalance(wb);

            // adjusts UI elements
            FStopLabel.Content = fstop;
            ExposureLabel.Content = exposure;
            ISOLabel.Content = iso;
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
            WhiteBalanceComboBox.Items.Add(""); // dummy choice for white balance

            // retrieves list of possible fstop, exposure, and iso settings
            AvList = MainCamera.GetSettingsList(PropertyID.Av);
            TvList = MainCamera.GetSettingsList(PropertyID.Tv);
            ISOList = MainCamera.GetSettingsList(PropertyID.ISO);

            // retrieves list of all possible white balance settings (other presets cannot be set, for some reason)
            WBList = new WhiteBalance[13]
            {
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
                WhiteBalance.WhitePaper2
            };

            // inserts all options into the comboboxes
            foreach (var Av in AvList) FStopComboBox.Items.Add(Av.StringValue);
            foreach (var Tv in TvList) ExposureComboBox.Items.Add(Tv.StringValue);
            foreach (var ISO in ISOList) ISOComboBox.Items.Add(ISO.StringValue);
            foreach (var WB in WBList) WhiteBalanceComboBox.Items.Add(WB.ToString());
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
            catch (Exception ex) { ShowMessage("red", "Error", ex.Message + ".\nPlease try toggling the Live View on and off."); }
        }


        /// <summary>
        /// Event handler triggered by the state of the camera changing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventID"></param>
        /// <param name="parameter"></param>
        private void MainCamera_StateChanged(Camera sender, StateEventID eventID, int parameter)
        {
            try { if (eventID == StateEventID.Shutdown) { Dispatcher.Invoke((Action)delegate { SetLiveViewOn(false); }); } }
            catch (Exception ex) { ShowMessage("red", "Error", ex.Message + ".\nPlease try toggling the Live View on and off."); }
        }


        /// <summary>
        /// Event handler triggered when the image is ready to be downloaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="Info"></param>
        private void MainCamera_DownloadReady(Camera sender, DownloadInfo Info)
        {
            // releases the shutter
            MainCamera.SendCommand(CameraCommand.PressShutterButton, (int)ShutterButton.OFF);

            // loads the collection and current index information
            Collection collection = CollectionList[CollectionComboBox.SelectedIndex - 1];
            int currIndex = IsRecapture ? HomeScreenPoseListView.SelectedIndex : CaptureNumber - 1;
            
            // saves the captured image
            // FORMAT: SAVEPATH \ RID \ DATE \ SESSION \ MODALITY \ DEVICE \ CAMERA PROFILE \ RID_DATE_COL#_SESSION_MODALITY_DEVICENAME_CAMERAPROFILE_FILENAME
            Pose pose = SessionPoseList[currIndex];
            string actionPath = SavePath + @"\ActionLog.txt";
            string filePath = ridDateCol[0] + "_" + ridDateCol[1] + "_" + collection.collectionNumber + "_" + SessionNumber.ToString("D1") + "_" + collection.modality + "_" + collection.deviceName + "_" + pose.cameraProfile + "_" + pose.filename;
            Info.FileName = filePath;
            sender.DownloadFile(Info, SavePath + @"\" + pose.cameraProfile);

            // logs the time of the capture
            if (!IsRecapture)
                File.AppendAllLines(actionPath, new[] { "Captured image - " + pose.filename + " @ " + DateTime.Now.ToString() });
            else
                File.AppendAllLines(actionPath, new[] { "Recaptured image - " + pose.filename + " @ " + DateTime.Now.ToString() });

            // updates the thumbnail in the HomeScreenPoseListView
            pose.thumbnail = SavePath + @"\" + pose.cameraProfile + @"\" + filePath;
            HomeScreenPoseListView.Items[currIndex] = UpdatePoseThumbnail(pose);

            // unlocks UI elements
            HomeScreenPoseListView.SelectedIndex = currIndex;
            IsRecapture = false;
            CaptureButton.IsEnabled = true;
            SessionSettingsAutofocusButton.IsEnabled = true;
            CameraControlsAutofocusButton.IsEnabled = true;
            CameraScreenAutofocusButton.IsEnabled = true;
            ToggleLiveViewHomeButton.Visibility = Visibility.Visible;
            CloseSessionButton.IsEnabled = true;
            if (HomeScreenPoseListView.SelectedIndex < CaptureNumber)
            {
                RecaptureButton.Visibility = Visibility.Visible;
                RecaptureAutofocusButton.Visibility = Visibility.Visible;
            }
        }


        /// <summary>
        /// Sets F-stop/aperture (Av) settings
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
                    MainCamera.SetSetting(PropertyID.Av, 0xffffffff);
                    break;
            }
        }


        /// <summary>
        /// Sets exposure/shutter speed (Tv) settings
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
                    MainCamera.SetSetting(PropertyID.Tv, 0xffffffff);
                    break;
            }
        }


        /// <summary>
        /// Sets ISO settings
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
                    MainCamera.SetSetting(PropertyID.ISO, 0xffffffff);
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


        const string CAMERACONFIGFILE = @"camera_profile_config.json";  // the filename of the camera configuration file
        List<CameraProfile> CameraProfileList;                          // a list of CameraProfiles


        /// <summary>
        /// Retrieves the list of camera profiles and loads them into the program
        /// </summary>
        private void LoadCameraProfiles()
        {
            // clears all visible camera profile lists
            CameraProfileListView.Items.Clear();
            PoseCameraProfileComboBox.Items.Clear();

            using (StreamReader r = new StreamReader(CAMERACONFIGFILE))
            {
                string json = r.ReadToEnd();
                CameraProfileList = JsonConvert.DeserializeObject<List<CameraProfile>>(json);

                // add each camera profile to the CameraProfilesListView and the PoseCameraProfileComboBox
                foreach (CameraProfile profile in CameraProfileList)
                {
                    // adds profile to the PoseCameraProfileComboBox
                    PoseCameraProfileComboBox.Items.Add(profile.name + " (" + profile.camera + ")");

                    // the profile name
                    // adds an extra underscore to display the access key
                    string name = profile.name.Replace("_", "__");
                    Label proName = new Label()
                    {
                        Content = "(" + profile.camera + ") " + name,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                    };

                    // the profile descriptors - first half
                    TextBlock proDesc1 = new TextBlock()
                    {
                        Text = "F-Stop: " + profile.fstop + ", Exposure: " + profile.exposure,
                        FontStyle = FontStyles.Italic,
                        TextWrapping = TextWrapping.WrapWithOverflow,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                        Margin = new Thickness(5, 0, 0, 0),
                    };

                    // the profile descriptors - second half
                    TextBlock proDesc2 = new TextBlock()
                    {
                        Text = "ISO: " + profile.iso + ", WhiteBalance: " + profile.whiteBalance,
                        FontStyle = FontStyles.Italic,
                        TextWrapping = TextWrapping.WrapWithOverflow,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                        Margin = new Thickness(5, 0, 0, 0),
                    };

                    // creates the StackPanel to hold all the content
                    StackPanel item = new StackPanel()
                    {
                        Height = 75,
                        Width = 255,
                        Cursor = Cursors.Hand
                    };
                    item.Children.Add(proName);
                    item.Children.Add(proDesc1);
                    item.Children.Add(proDesc2);

                    // adds the collection StackPanel to the CollectionListView
                    CameraProfileListView.Items.Add(item);
                }

                // closes the stream
                r.Close();
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



        #region Collections
        // ================================================================== COLLECTIONS ================================================================== //


        const string COLLECTIONCONFIGFILE = @"collection_config.json";  // the filename of the collection configuration file
        List<Collection> CollectionList;                                // a list of Collections
        List<Pose> PoseList;                                            // a list of Poses


        /// <summary>
        /// Retrieves the list of collections and loads them into the program
        /// </summary>
        private void LoadCollections()
        {
            // sets the selected collection to nothing to avoid errors
            CollectionComboBox.SelectedIndex = 0;

            // clears the CollectionListView and CollectionComboBox
            CollectionListView.Items.Clear();
            for (int i = CollectionComboBox.Items.Count - 1; i > 0; i--)
            {
                CollectionComboBox.Items.RemoveAt(i);
            }

            using (StreamReader r = new StreamReader(COLLECTIONCONFIGFILE))
            {
                string json = r.ReadToEnd();
                CollectionList = JsonConvert.DeserializeObject<List<Collection>>(json);

                // add each collection to the CollectionComboBox
                foreach (Collection collection in CollectionList)
                {
                    CollectionComboBox.Items.Add("(#" + collection.collectionNumber + ") " + collection.name);

                    // the collection name
                    // adds an extra underscore to display the access key
                    string colName = collection.name.Replace("_", "__");
                    Label colNameAndNo = new Label()
                    {
                        Content = "(#" + collection.collectionNumber + ") " + colName,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                    };

                    // the collection camera
                    TextBlock colCamera = new TextBlock()
                    {
                        Text = "Camera: " + collection.camera,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    // the collection savepath
                    // adds an extra underscore to display the access key
                    string savePath = collection.savingDirectory.Replace("_", "__");
                    TextBlock colSavePath = new TextBlock()
                    {
                        Text = "Save path: " + savePath,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    // the collection descriptors
                    TextBlock colDesc = new TextBlock()
                    {
                        Text = "Poses: " + collection.numberOfPoses + ", Device Name: " + collection.deviceName,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    // creates the StackPanel to hold all the content
                    StackPanel item = new StackPanel()
                    {
                        Height = 75,
                        Width = 320,
                        Cursor = Cursors.Hand
                    };
                    item.Children.Add(colNameAndNo);
                    item.Children.Add(colCamera);
                    item.Children.Add(colSavePath);
                    item.Children.Add(colDesc);

                    // adds the collection StackPanel to the CollectionListView
                    CollectionListView.Items.Add(item);

                }

                // closes the stream
                r.Close();
            }
        }


        /// <summary>
        /// Displays the pose information on the CollectionSCreenPoseListView
        /// </summary>
        private void LoadCollectionPoseList()
        {
            try
            {
                CollectionScreenPoseListView.Items.Clear();

                //foreach (Pose pose in PoseList)
                for (int i = 0; i < PoseList.Count; i++)
                {
                    Pose pose = PoseList[i];

                    // colors each row
                    SolidColorBrush background;
                    if (i % 2 == 0)
                        background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 242, 242, 242));
                    else
                        background = System.Windows.Media.Brushes.Transparent;
                    
                    // StackPanel to hold all the content
                    StackPanel item = new StackPanel()
                    {
                        Orientation = Orientation.Horizontal,
                        Height = 45,
                        Width = PoseSettingsGridBackground.Width,
                        Cursor = Cursors.Hand,
                        Background = background
                    };

                    // the pose number
                    Label poseNo = new Label()
                    {
                        Content = i + 1,
                        Width = 30,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        BorderThickness = new Thickness(0)
                    };

                    // item separator 1
                    Line separator1 = new Line()
                    {
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 166, 166, 166)),
                        X1 = 7,
                        X2 = 7,
                        Y1 = 0,
                        Y2 = 45,
                        Width = 15,
                    };

                    // the pose thumbnail
                    System.Windows.Controls.Image poseThumbnailPanel = new System.Windows.Controls.Image()
                    {
                        Source = LoadImage(pose.thumbnail, 45),
                        RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                        RenderTransform = new RotateTransform(90),
                        Width = 30,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    // item separator 2
                    Line separator2 = new Line()
                    {
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 166, 166, 166)),
                        X1 = 7,
                        X2 = 7,
                        Y1 = 0,
                        Y2 = 45,
                        Width = 15,
                    };

                    // the pose title
                    Label poseTitle = new Label()
                    {
                        Content = pose.title,
                        Height = 30,
                        Width = 100,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Background = background,
                        BorderThickness = new Thickness(0)
                    };

                    // item separator 3
                    Line separator3 = new Line()
                    {
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 166, 166, 166)),
                        X1 = 7,
                        X2 = 7,
                        Y1 = 0,
                        Y2 = 45,
                        Width = 15,
                    };

                    // the pose description
                    Label poseDesc = new Label()
                    {
                        Content = pose.description,
                        Height = 30,
                        Width = 150,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Background = background,
                        BorderThickness = new Thickness(0)
                    };

                    // item separator 4
                    Line separator4 = new Line()
                    {
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 166, 166, 166)),
                        X1 = 7,
                        X2 = 7,
                        Y1 = 0,
                        Y2 = 45,
                        Width = 15,
                    };

                    // the pose camera profile
                    Label poseCameraProfile = new Label()
                    {
                        Content = pose.cameraProfile,
                        Height = 30,
                        Width = 100,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        BorderThickness = new Thickness(0)
                    };

                    // item separator 5
                    Line separator5 = new Line()
                    {
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 166, 166, 166)),
                        X1 = 7,
                        X2 = 7,
                        Y1 = 0,
                        Y2 = 45,
                        Width = 15,
                    };

                    // the pose filename
                    // adds an extra underscore to display the access key
                    string filename = pose.filename.Replace("_", "__");
                    Label poseFileName = new Label()
                    {
                        Content = filename,
                        Height = 30,
                        Width = 150,
                        HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Background = background,
                        BorderThickness = new Thickness(0)
                    };

                    // adds all of the content to the StackPanel item
                    item.Children.Add(poseNo);
                    item.Children.Add(separator1);
                    item.Children.Add(poseThumbnailPanel);
                    item.Children.Add(separator2);
                    item.Children.Add(poseTitle);
                    item.Children.Add(separator3);
                    item.Children.Add(poseDesc);
                    item.Children.Add(separator4);
                    item.Children.Add(poseCameraProfile);
                    item.Children.Add(separator5);
                    item.Children.Add(poseFileName);

                    // adds the collection StackPanel to the CollectionListView
                    CollectionScreenPoseListView.Items.Add(item);
                }
            }
            catch (Exception ex) { ShowMessage("red", "Nothing to load.", ex.Message); }
        }


        /// <summary>
        /// Details of the collection
        /// </summary>
        class Collection
        {
            public string name { get; set; }                // the collection name
            public string collectionNumber { get; set; }    // the collection number
            public int numberOfPoses { get; set; }          // the number of poses in the collection
            public string savingDirectory { get; set; }     // the save path of the collection
            public string deviceName { get; set; }          // the device name for filename purposes
            public string modality { get; set; }            // the modality (face, hand, fingerprint, etc.)
            public string camera { get; set; }              // the camera associated with the collection
            public List<Pose> poses { get; set; }           // the list of camera profiles that make up the collection


            /// <summary>
            /// Default constructor for a collection
            /// </summary>
            public Collection()
            {
                name = null;
                collectionNumber = null;
                numberOfPoses = 0;
                savingDirectory = null;
                deviceName = null;
                modality = null;
                camera = null;
                poses = null;
            }


            /// <summary>
            /// Constructor for a collection
            /// </summary>
            /// <param name="nameVal">the collection name</param>
            /// <param name="colNum">the collection number</param>
            /// <param name="nrPoses">the number of poses in the collection</param>
            /// <param name="savePath">the save path of the collection</param>
            public Collection(string nameVal, string colNum, int nrPoses, string savePath, string devName, string modName, string camVal)
            {
                name = nameVal;
                collectionNumber = colNum;
                numberOfPoses = nrPoses;
                savingDirectory = savePath;
                deviceName = devName;
                modality = modName;
                camera = camVal;
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
            public string title { get; set; }           // the pose title
            public string description { get; set; }     // the description of the pose
            public string thumbnail { get; set; }       // the thumbnail for the pose
            public string filename { get; set; }        // the filename for the pose
            public string cameraProfile { get; set; }   // the camera profile for the pose


            /// <summary>
            /// Default constructor for a pose
            /// </summary>
            public Pose()
            {
                title = null;
                description = null;
                thumbnail = null;
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
            public Pose(string titleVal, string descVal, string thumbSource, string filnamVal, string camProfile)
            {
                title = titleVal;
                description = descVal;
                thumbnail = thumbSource;
                filename = filnamVal;
                cameraProfile = camProfile;
            }
        }


        #endregion



        #region UI Functions
        // ================================================================== UI FUNCTIONS ================================================================== //           


        /// <summary>
        /// Switches between window screens
        /// </summary>
        /// <param name="screen"></param>
        private void SwitchScreen(string screen)
        {

            var bc = new BrushConverter();
            if (screen == "home")
            {
                // turn on home live view
                if (CameraComboBox.SelectedIndex > 0)
                {
                    HomeLiveViewCanvas.Background = LiveViewBrush;
                    CameraLiveViewCanvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 0, 0));
                }

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
                // turn on camera live view
                if (CameraComboBox.SelectedIndex > 0)
                {
                    HomeLiveViewCanvas.Background = System.Windows.Media.Brushes.Transparent;
                    CameraLiveViewCanvas.Background = LiveViewBrush;
                }
                else
                    CameraLiveViewCanvas.Background = System.Windows.Media.Brushes.Black;

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
                    MessageBarLabel.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#BE3A34");
                    MessageBarLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                }
                else if (color == "yellow")
                {
                    MessageBarLabel.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#FDDA24");
                    MessageBarLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#000000");
                }
                else if (color == "green")
                {
                    MessageBarLabel.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#9ABEAA");
                    MessageBarLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#000000");
                }

                // clears text before writing new message
                MessageTextBlock.Text = null;

                // fills in the title and the message content
                MessageBarLabel.Content = title;
                MessageTextBlock.Text = message;
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


        private const int HIGHRESOLUTION = 375; // the height of a high resolution thumbnail
        private const int LOWRESOLUTION = 183;  // the height of a low resolution thumbnail
        private List<Pose> SessionPoseList;     // the list of poses for one session
        private bool IsSessionOngoing = false;  // true if the session is ongoing
        private bool IsRecapture;               // true if the photo taken is a recapture
        private int CaptureNumber;              // int which stores the index of the current capture in the collection
        private string SavePath;                // string storing the folder of the current session
        private string[] ridDateCol;            // array that holds the RID, date, and collection number
        private int SessionNumber;              // int that counts the iteration of the current session


        /// <summary>
        /// Begins the session
        /// </summary>
        private void BeginSession()
        {
            Collection collection = CollectionList.ElementAt(CollectionComboBox.SelectedIndex - 1);

            // logs the beginning of a session
            File.AppendAllLines(SavePath + @"\ActionLog.txt", new[] { "Subject RID: " + ridDateCol[0] + "\n Session Start: " + DateTime.Now.ToString() });

            // clones the collection pose list to SessionPoseList
            SessionPoseList = collection.poses.ConvertAll(pose => new Pose(
                pose.title,
                pose.description,
                pose.thumbnail,
                pose.filename,
                pose.cameraProfile));

            // adjusts UI
            SessionSettingsGrid.Visibility = Visibility.Hidden;
            SessionSettingsGrid.Visibility = Visibility.Hidden;
            CameraControlsGrid.Visibility = Visibility.Visible;
            CameraSettings_ApplyChangesButton.IsEnabled = false;

            // sets up the camera
            MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
            MainCamera.SetCapacity(999999999, int.MaxValue);

            // begins session
            IsSessionOngoing = true;
            CaptureNumber = 0;
            UpdateSession();
        }


        /// <summary>
        /// Updates the session
        /// </summary>
        private void UpdateSession()
        {
            if (CaptureNumber < SessionPoseList.Count)
            {
                Pose pose = SessionPoseList[CaptureNumber];
                NextImage.Source = LoadImage(pose.thumbnail, 100);
                NextPoseTitleLabel.Content = pose.title;
                NextPoseDescLabel.Content = pose.description;
            }
            else
            {
                NextImage.Source = null;
                NextPoseTitleLabel.Content = null;
                NextPoseDescLabel.Content = null;
            }
        }


        /// <summary>
        /// Resets everything to before a session
        /// </summary>
        private void EndSession()
        {
            // resets variables
            CaptureNumber = 0;
            SessionPoseList = null;
            ridDateCol = null;
            IsSessionOngoing = false;

            // adjusts the UI
            CaptureButton.Content = "\uE114";
            CaptureButton.ToolTip = "Capture photo. Right click and hold to focus camera.";
            SelectedImage.Source = null;
            NextImage.Source = null;
            SelectedPoseTitleLabel.Content = null;
            SelectedPoseDescLabel.Content = null;
            SessionSettingsGrid.Visibility = Visibility.Visible;
            if (IsCameraOn)
                SessionSettingsAutofocusButton.Visibility = Visibility.Visible;
            CameraControlsGrid.Visibility = Visibility.Hidden;
            RecaptureButton.Visibility = Visibility.Hidden;
            RecaptureAutofocusButton.Visibility = Visibility.Hidden;
            RIDTextBox.Clear();
            CollectionComboBox.SelectedIndex = 0;
            LoadHomePoseList();
            if (PoseList != null)
                if(PoseList.Count > 0)
                    SaveCollectionButton.IsEnabled = true;
            CameraSettings_ApplyChangesButton.IsEnabled = true;
        }


        /// <summary>
        /// Populates the PoseListView with collection thumbnails
        /// </summary>
        private void LoadHomePoseList()
        {
            HomeScreenPoseListView.Items.Clear();

            if (CollectionComboBox.SelectedIndex != 0)
            {
                //Collection collection = CollectionList.ElementAt(CollectionComboBox.SelectedIndex - 1);
                List<Pose> poses = CollectionList.ElementAt(CollectionComboBox.SelectedIndex - 1).poses;

                // adds the pose to the PoseListView
                foreach (Pose pose in poses)
                    HomeScreenPoseListView.Items.Add(UpdatePoseThumbnail(pose));
            }
        }


        /// <summary>
        /// Updates the pose thumbnail specified
        /// </summary>
        /// <param name="index">the index of the thumbnail that must be updated</param>
        private StackPanel UpdatePoseThumbnail(Pose pose)
        {
            // creates the StackPanel to hold all the content
            StackPanel item = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Height = 160,
                Width = 275,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 41, 53, 62)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };

            // creates background grid for thumbnail
            Grid backgroundThumbnail = new Grid()
            {
                Height = 150,
                Width = 100,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 29, 42, 52)),
                Margin = new Thickness(5, 0, 5, 0)
            };

            // the pose thumbnail
            System.Windows.Controls.Image poseThumbnail = new System.Windows.Controls.Image()
            {
                Source = LoadImage(pose.thumbnail, 60),
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new RotateTransform(90),
                Height = 100,
                Margin = new Thickness(-25, 0, -25, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            // a StackPanel to hold the pose descriptions
            StackPanel descPanel = new StackPanel()
            {
                VerticalAlignment = VerticalAlignment.Center,
            };

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

            // the pose filename
            Label poseProfile = new Label()
            {
                Content = "Camera profile: " + pose.cameraProfile,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                FontSize = 10,
            };

            // the pose filename
            Label poseFilename = new Label()
            {
                Content = "Filename: " + pose.filename,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                FontSize = 10,
            };

            // adds all of the elements into one StackPanel
            descPanel.Children.Add(poseTitle);
            descPanel.Children.Add(poseDesc);
            descPanel.Children.Add(poseProfile);
            descPanel.Children.Add(poseFilename);
            backgroundThumbnail.Children.Add(poseThumbnail);
            item.Children.Add(backgroundThumbnail);
            item.Children.Add(descPanel);

            // returns the collection StackPanel
            return item;
        }


        /// <summary>
        /// Loads a BitmapImage
        /// </summary>
        /// <param name="directory">the filepath of the image to be loaded</param>
        /// <param name="imageHeight">the height resolution of the image</param>
        private BitmapImage LoadImage(string directory, int imageHeight)
        {
            BitmapImage source = new BitmapImage();

            FileStream f;
            try
            {
                f = File.OpenRead(directory);
            }
            catch
            {
                return null;
            }
            MemoryStream ms = new MemoryStream();

            f.CopyTo(ms);
            ms.Seek(0, System.IO.SeekOrigin.Begin);
            f.Close();

            source.BeginInit();
            source.StreamSource = ms;
            source.DecodePixelHeight = imageHeight;
            source.DecodePixelWidth = imageHeight * (2 / 3);
            source.DecodeFailed += Source_DecodeFailed;
            source.EndInit();

            return source;
        }


        /// <summary>
        /// Displays an error when the image fails to load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Source_DecodeFailed(object sender, ExceptionEventArgs e)
        {
            ShowMessage("red", "Error", "Failed to load image.");
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

            if(IsSessionOngoing)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to exit in the middle of a session?", "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No) e.Cancel = true;
            }
        }


        #endregion



        #region Home Screen Event Handlers
        // ================================================================== HOME SCREEN EVENT HANDLERS ================================================================== //


        /// <summary>
        /// Event handler that toggles the Live View
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleLiveViewHomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!MainCamera.SessionOpen)
            {
                SetLiveViewOn(true);
                if (SessionSettingsGrid.Visibility == Visibility.Visible)
                    SessionSettingsAutofocusButton.Visibility = Visibility.Visible;
            }
            else
                SetLiveViewOn(false);
        }


        /// <summary>
        /// Event handler that changes the selection within the Camera ComboBox and turns on the selected camera
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // if a camera is selected, turn on the live view and display all of the camera settings
            if (CameraComboBox.SelectedIndex != 0)
            {
                // turns on the live view
                ToggleLiveViewHomeButton.Visibility = Visibility.Visible;
                SessionSettingsAutofocusButton.Visibility = Visibility.Visible;
                SetLiveViewOn(true);
                InitializeCameraSettings();

                // selects the current settings on the Camera Screen
                FStopComboBox.SelectedIndex = FStopComboBox.Items.IndexOf(AvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Av)).StringValue);
                ExposureComboBox.SelectedIndex = ExposureComboBox.Items.IndexOf(TvValues.GetValue(MainCamera.GetInt32Setting(PropertyID.Tv)).StringValue);
                ISOComboBox.SelectedIndex = ISOComboBox.Items.IndexOf(ISOValues.GetValue(MainCamera.GetInt32Setting(PropertyID.ISO)).StringValue);
                WhiteBalanceComboBox.SelectedIndex = WhiteBalanceComboBox.Items.IndexOf(MainCamera.GetStringSetting(PropertyID.WhiteBalance));

                // deselects any camera profile previously selected on the camera screen
                CameraProfileListView.SelectedItem = null;
            }
            // if a camera is not selected, turn off the live view
            else
            {
                if (ToggleLiveViewHomeButton != null)
                    ToggleLiveViewHomeButton.Visibility = Visibility.Hidden;
                SetLiveViewOn(false);
            }
        }


        /// <summary>
        /// Event handler that loads a new collection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadHomePoseList();
        }


        /// <summary>
        /// Event handler that checks for session conditions, and then begins a session if the conditions are met
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void EnterRIDButton_Click(object sender, RoutedEventArgs e)
        {

            // converts the RID input to lowercase and splits it with "_"
            string input = RIDTextBox.Text.ToLower();
            ridDateCol = input.Split('_');
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
                Collection collection = CollectionList.ElementAt(CollectionComboBox.SelectedIndex - 1);

                // assigns the session attributes
                rid = ridDateCol[0];
                date = ridDateCol[1];
                col = ridDateCol[2];

                // verifies that RID, Date, and Collection Number are the correct number of digits
                if (rid.Length != 7 || date.Length != 8 || col.Length <= 0)
                {
                    ShowMessage("red", "Invalid input", "RID_Date_Col must be in the following format: <7 DIGITS>_<8 DIGITS>_<1 OR MORE DIGITS>");
                    return;
                }

                // verifies that the collection number matches the selected collection
                if (col != collection.collectionNumber)
                {
                    ShowMessage("red", "Mismatched collections", "The collection number in the RID does not match that of the selected collection." +
                        "\nPlease try entering the RID again or choosing a different collection.");
                    return;
                }

                // verifies that the collection is compatible with camera
                if (collection.camera != CameraComboBox.Text)
                {
                    ShowMessage("red", "Invalid camera", "The connected camera is not associated with this collection.");
                    return;
                }

                // verifies if camera profiles in the collection exist
                List<CameraProfile> profiles = new List<CameraProfile>();
                foreach (Pose pose in collection.poses)
                {
                    CameraProfile item = CameraProfileList.FirstOrDefault(profile => pose.cameraProfile == profile.name);
                    if (item != null)
                        profiles.Add(item);
                    else
                    {
                        ShowMessage("red", "Invalid camera profile",
                            "There is an invalid camera profile in the \"" + collection.name + "\" Collection." +
                            "\nPlease remove camera profile \"" + pose.cameraProfile + "\"from the collection on the Collections Screen."
                            );
                        return;
                    }
                }

                // verifies if camera profiles are compatible with the connected camera
                foreach (CameraProfile profile in profiles)
                {
                    if (profile.camera != CameraComboBox.Text)
                    {
                        ShowMessage("red", "Camera profile and camera are incompatible",
                            "\"" + collection.name + "\"uses the camera profile \"" + profile.name + "\", which is incompatible with " + CameraComboBox.Text +
                            ".\nPlease edit \"" + collection.name + "\" in the Collections Screen.");
                        return;
                    }
                }

                // verifies if collection directory still exists
                if (!Directory.Exists(collection.savingDirectory))
                {
                    ShowMessage("red", "Invalid save directory.", "The directory specified for \"" + collection.name + "\" no longer exists. " +
                        "Please edit \"" + collection.name + "\" in the Collections Screen.");
                    return;
                }

                // if all conditions are met, resume with folder creation
                // increments the SessionNumber if sessions already exist
                DirectoryInfo dir1 = new DirectoryInfo(collection.savingDirectory + @"\" + ridDateCol[0] + @"\" + ridDateCol[1]);
                SessionNumber = (Directory.Exists(dir1.ToString())) ? (dir1.GetDirectories().Length + 1) : 1;
                if(SessionNumber > 1)
                {
                    MessageBoxResult result = MessageBox.Show("There already exists a session folder here. Do you want to overwrite this session?" +
                        "\nPress \"No\" to continue with a brand new session.", "Warning!", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                        SessionNumber--;
                }
                SavePath = collection.savingDirectory + @"\" + ridDateCol[0] + @"\" + ridDateCol[1] + @"\" + SessionNumber + @"\" + collection.modality + @"\" + collection.deviceName;
                Directory.CreateDirectory(SavePath);

                // checks if there are any files in the SavePath
                DirectoryInfo dir2 = new DirectoryInfo(SavePath);
                if (Directory.Exists(dir2.ToString()) && (dir2.GetDirectories().Length > 0 || dir2.GetFiles().Length > 0))
                    ShowMessage("yellow", "Warning", "Participant already has session files for this session. Files may be overwritten if you continue.");

                // begin a new session
                BeginSession();
                SessionSettingsAutofocusButton.Visibility = Visibility.Hidden;
            }
            else
                ShowMessage("red", "Invalid input", "Input must be in the following form: <RID>_<DATE>_<COLLECTION NUMBER>");
        }


        /// <summary>
        /// Event handler that begins a new session
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RIDTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                EnterRIDButton_Click(sender, e);
        }


        /// <summary>
        /// Event handler that takes a photo
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            // if the session has not reached the end, adjust the camera settings, take a picture, and update the session
            if (CaptureNumber < SessionPoseList.Count)
            {
                CameraProfile item = CameraProfileList.FirstOrDefault(profile => SessionPoseList[CaptureNumber].cameraProfile == profile.name);
                if (item != null)
                    SetCameraSettings(item.fstop, item.exposure, item.iso, item.whiteBalance);
                TakePicture();
            }

            CaptureNumber++;
            UpdateSession();

            // if the session has reached the end, adjust the CaptureButton content
            if (CaptureNumber == SessionPoseList.Count)
            {
                CaptureButton.Content = "\u21B6";
                CaptureButton.ToolTip = "End session.";
            }

            // ends the session
            else if (CaptureNumber > SessionPoseList.Count)
                EndSession();
        }



        /// <summary>
        /// Event handler that autofocuses the camera if CaptureButton is right click pressed and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CaptureButton_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfile item = CameraProfileList.FirstOrDefault(profile => SessionPoseList[CaptureNumber].cameraProfile == profile.name);
            if (item != null)
                SetCameraSettings(item.fstop, item.exposure, item.iso, item.whiteBalance);
            FocusCamera();
        }


        /// <summary>
        /// Event handler that unfocuses the camera once the CaptureButton is right click released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CaptureButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnfocusCamera();
        }


        /// <summary>
        /// Event handler that autofocuses the camera if CameraControlsAutofocusButton is pressed and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraControlsAutofocusButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfile item = CameraProfileList.FirstOrDefault(profile => SessionPoseList[CaptureNumber].cameraProfile == profile.name);
            if (item != null)
                SetCameraSettings(item.fstop, item.exposure, item.iso, item.whiteBalance);
            FocusCamera();
        }


        /// <summary>
        /// Event handler that unfocuses the camera once the CameraControlsAutofocusButton is released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraControlsAutofocusButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnfocusCamera();
        }


        /// <summary>
        /// Event handler that opens the folder of the current session
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenContainingFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Collection collection = CollectionList[CollectionComboBox.SelectedIndex - 1];
            if (Directory.Exists(SavePath)) System.Diagnostics.Process.Start(SavePath);
        }


        /// <summary>
        /// Event handler that autofocuses the camera if SessionSettingsAutofocusButton is pressed and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SessionSettingsAutofocusButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FocusCamera();
        }


        /// <summary>
        /// Event handler that unfocuses the camera once the SessionSettingsAutofocusButton is released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SessionSettingsAutofocusButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnfocusCamera();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecaptureButton_Click(object sender, RoutedEventArgs e)
        {
            IsRecapture = true;
            CameraProfile item = CameraProfileList.FirstOrDefault(profile => SessionPoseList[HomeScreenPoseListView.SelectedIndex].cameraProfile == profile.name);
            if (item != null)
                SetCameraSettings(item.fstop, item.exposure, item.iso, item.whiteBalance);
            TakePicture();
        }


        /// <summary>
        /// Event handler that autofocuses the camera if RecaptureButton is right click pressed and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecaptureButton_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfile item = CameraProfileList.FirstOrDefault(profile => SessionPoseList[HomeScreenPoseListView.SelectedIndex].cameraProfile == profile.name);
            if (item != null)
                SetCameraSettings(item.fstop, item.exposure, item.iso, item.whiteBalance);
            FocusCamera();
        }


        /// <summary>
        /// Event handler that unfocuses the camera once the RecaptureButton is right click released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecaptureButton_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnfocusCamera();
        }


        /// <summary>
        /// Event handler that focuses the camera once the RecaptureAutofocusButton is pressed and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecaptureAutofocusButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfile item = CameraProfileList.FirstOrDefault(profile => SessionPoseList[HomeScreenPoseListView.SelectedIndex].cameraProfile == profile.name);
            if (item != null)
                SetCameraSettings(item.fstop, item.exposure, item.iso, item.whiteBalance);
            FocusCamera();
        }


        /// <summary>
        /// Event handler that unfocuses the camera once the RecaptureAutofocusButton is released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecaptureAutofocusButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnfocusCamera();
        }


        /// <summary>
        /// Event handler that closes a current session
        /// </summary>
        /// <param na me="sender"></param>
        /// <param name="e"></param>
        private void CloseSessionButton_Click(object sender, RoutedEventArgs e)
        {
            EndSession();
        }


        /// <summary>
        /// Event handler that loads the image of the selected pose
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HomeScreenPoseListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsSessionOngoing)
            {
                if (HomeScreenPoseListView.SelectedIndex < 0)
                    HomeScreenPoseListView.SelectedIndex = 0;
                Pose pose = SessionPoseList[HomeScreenPoseListView.SelectedIndex];
                SelectedImage.Source = LoadImage(pose.thumbnail, 350);
                SelectedPoseTitleLabel.Content = pose.title;
                SelectedPoseDescLabel.Content = pose.description;
                if (HomeScreenPoseListView.SelectedIndex < CaptureNumber)
                {
                    RecaptureButton.Visibility = Visibility.Visible;
                    RecaptureAutofocusButton.Visibility = Visibility.Visible;
                }
                else
                {
                    RecaptureButton.Visibility = Visibility.Hidden;
                    RecaptureAutofocusButton.Visibility = Visibility.Hidden;
                }
            }
            HomeScreenPoseListView.ScrollIntoView(HomeScreenPoseListView.SelectedItem);
        }


        /// <summary>
        /// Event handler that allows user to scroll the PoseListView using the mousewheel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HomeScreenPoseListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            ScrollViewer scrollviewer = FindVisualChildren<ScrollViewer>(listBox).FirstOrDefault();
            if (e.Delta > 0)
                scrollviewer.LineLeft();
            else
                scrollviewer.LineRight();
            e.Handled = true;
        }


        /// <summary>
        /// Iterable object which allows the horizontal wheel scrolling to work in PoseListView
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="depObj"></param>
        /// <returns></returns>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                        yield return (T)child;
                    foreach (T childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }


        #endregion



        #region Camera Screen Event Handlers
        // ================================================================== CAMERA SCREEN EVENT HANDLERS ================================================================== //


        /// <summary>
        /// Event handler that deselects selected profile when pressing the FStopComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FStopComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfileListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that deselects selected profile when pressing the ExposureComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExposureComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfileListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that deselects selected profile when pressing the ISOComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ISOComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfileListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that deselects selected profile when pressing the WhiteBalanceComboBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WhiteBalanceComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfileListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that deselects selected profile when pressing the CameraProfileNameTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraProfileNameTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CameraProfileListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that applies the changes to the live view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraSettings_ApplyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            CameraProfileListView.SelectedItem = null;

            // if all required fields are filled (f-stop, exposure, iso), changes are applied
            if (FStopComboBox.SelectedItem != null &&
                ExposureComboBox.SelectedItem != null &&
                ISOComboBox.SelectedItem != null)
            {
                // applies changes
                if (WhiteBalanceComboBox.SelectedItem != null)
                    SetCameraSettings(
                        FStopComboBox.SelectedItem.ToString(),
                        ExposureComboBox.SelectedItem.ToString(),
                        ISOComboBox.SelectedItem.ToString(),
                        WhiteBalanceComboBox.SelectedItem.ToString());
                else
                    SetCameraSettings(
                        FStopComboBox.SelectedItem.ToString(),
                        ExposureComboBox.SelectedItem.ToString(),
                        ISOComboBox.SelectedItem.ToString());

                // turns off live view and then turns it back on
                SetLiveViewOn(false);
                System.Threading.Thread.Sleep(250);
                SetLiveViewOn(true);
            }
            // if required fields are not filled, throws an error
            else
                ShowMessage("red", "Unfilled fields", "F-Stop/Aperture, Exposure/Shutter Speed, and ISO must be filled before applying changes.");
        }


        /// <summary>
        /// Event handler that saves the camera settings into a new camera profile
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveCameraProfileButton_Click(object sender, RoutedEventArgs e)
        {
            CameraProfileListView.SelectedItem = null;
            string profileName = CameraProfileNameTextBox.Text;

            // if all required fields are filled (f-stop, exposure, iso), changes are applied
            if (FStopComboBox.SelectedItem != null &&
                ExposureComboBox.SelectedItem != null &&
                ISOComboBox.SelectedItem != null &&
                profileName.Length > 0)
            {
                // checks if profile name is valid
                var regex = new Regex(@"[.,/;'\[\]\\`<>?:\""{}|~!@#$%^&*()+= ]+");
                if (regex.IsMatch(profileName))
                {
                    ShowMessage("red", "Invalid profile name", "Cannot use spaces or special characters.");
                    return;
                }

                // makes a copy list for camera profiles
                List<CameraProfile> newList = new List<CameraProfile>();
                foreach (CameraProfile pro in CameraProfileList)
                    newList.Add(pro);

                // checks for repeat names
                foreach (CameraProfile pro in CameraProfileList)
                {
                    if (pro.name.ToLower() == profileName.ToLower())
                    {
                        // verifies whether user wants to replace an existing camera profile
                        MessageBoxResult result = MessageBox.Show("A profile with this name already exists. Do you want to overwrite it?", "Warning!",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            newList.Remove(pro);
                            break;
                        }
                        else
                            return;
                    }
                }

                // creates a brand new camera profile and enters the specified data
                CameraProfile profile = new CameraProfile();
                profile.name = CameraProfileNameTextBox.Text;
                profile.camera = MainCamera.DeviceName;
                profile.fstop = FStopComboBox.SelectedItem.ToString();
                profile.exposure = ExposureComboBox.SelectedItem.ToString();
                profile.iso = ISOComboBox.SelectedItem.ToString();
                if (WhiteBalanceComboBox.SelectedItem != null)
                    profile.whiteBalance = WhiteBalanceComboBox.SelectedItem.ToString();

                // adds the profile to the camera profile list and writes it to the configuration file
                newList.Add(profile);
                CameraProfileList = newList.OrderBy(pro => pro.name).ThenBy(pro => pro.camera).ToList();
                string output = JsonConvert.SerializeObject(CameraProfileList, Formatting.Indented);
                File.WriteAllText(CAMERACONFIGFILE, output);
                LoadCameraProfiles();
            }
            // if required fields are not filled, throws an error
            else
                ShowMessage("red", "Unfilled fields", "F-Stop/Aperture, Exposure/Shutter Speed, ISO, and profile name must be filled before saving profile.");
        }


        /// <summary>
        /// Event handler that updates camera profile settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraProfileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // if a camera is connected, load settings
            if (CameraComboBox.SelectedIndex != 0 && CameraProfileListView.SelectedItem != null)
            {
                // if the profile camera matches the connected device name, load settings
                if (CameraProfileList.ElementAt(CameraProfileListView.SelectedIndex).camera == MainCamera.DeviceName)
                {
                    // retrieves the information from the camera profile config file
                    CameraProfile profile = CameraProfileList.ElementAt(CameraProfileListView.SelectedIndex);

                    // inserts camera profile information
                    FStopComboBox.SelectedItem = profile.fstop;
                    ExposureComboBox.SelectedItem = profile.exposure;
                    ISOComboBox.SelectedItem = profile.iso;
                    WhiteBalanceComboBox.SelectedItem = profile.whiteBalance;
                }
                // if the camera connected does not match the device name, throw an error
                else
                    ShowMessage("yellow", "Warning: Specified camera not connected.", "Only camera profiles registered with the connected camera can be loaded.");
            }
            // if a camera is not connected, throw an error
            else if (CameraProfileListView.SelectedItem != null)
                ShowMessage("yellow", "Warning: No camera selected", "Camera must be connected to load camera settings.");
        }


        /// <summary>
        /// Event handler that deletes the selected camera profile
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteCameraProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // if a camera profile is selected, delete the profile
            if (CameraProfileListView.SelectedItem != null)
            {
                // verifies whether a user wants to delete the selected camera profile
                MessageBoxResult result = MessageBox.Show("This will delete the selected camera profile permanently. Are you sure you want to delete it?", "Warning!",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;

                // makes a copy list for camera profiles
                List<CameraProfile> newList = new List<CameraProfile>();
                foreach (CameraProfile profile in CameraProfileList)
                    newList.Add(profile);

                // removes the camera profile
                newList.Remove(CameraProfileList.ElementAt(CameraProfileListView.SelectedIndex));

                // replaces the original CameraProfileList with the newList
                CameraProfileList = newList.OrderBy(pro => pro.name).ThenBy(pro => pro.camera).ToList();

                // converts list back to JSON, writes to configuration file, and refreshes camera profiles list
                string output = JsonConvert.SerializeObject(CameraProfileList.ToArray(), Formatting.Indented);
                File.WriteAllText(CAMERACONFIGFILE, output);
                LoadCameraProfiles();
            }
            // if a camera profile is not selected, throw an error
            else
                ShowMessage("red", "No camera profile selected", "Please select a camera profile to delete first.");
        }


        /// <summary>
        /// Event handler that autofocuses the camera if button is pressed and held
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraScreenAutofocusButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FocusCamera();
        }


        /// <summary>
        /// Event handler that unfocuses the camera once the button is released
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CameraScreenAutofocusButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            UnfocusCamera();
        }
        
        
        /// <summary>
        /// Event handler that toggles the reference frames on the live view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleReferenceFrames(object sender, RoutedEventArgs e)
        {
            if (SubjectOuterBoundingBoxCheckBox.IsChecked ?? false)
            {
                OuterBoxHomeScreen.Visibility = Visibility.Visible;
                OuterBoxCameraScreen.Visibility = Visibility.Visible;
            }
            else
            {
                OuterBoxHomeScreen.Visibility = Visibility.Hidden;
                OuterBoxCameraScreen.Visibility = Visibility.Hidden;
            }

            if (SubjectInnerBoundingBoxCheckBox.IsChecked ?? false)
            {
                InnerBoxHomeScreen.Visibility = Visibility.Visible;
                InnerBoxCameraScreen.Visibility = Visibility.Visible;
            }
            else
            {
                InnerBoxHomeScreen.Visibility = Visibility.Hidden;
                InnerBoxCameraScreen.Visibility = Visibility.Hidden;
            }

            if (EyeBoxCheckBox.IsChecked ?? false)
            {
                EyeBoxHomeScreen.Visibility = Visibility.Visible;
                EyeBoxCameraScreen.Visibility = Visibility.Visible;
            }
            else
            {
                EyeBoxHomeScreen.Visibility = Visibility.Hidden;
                EyeBoxCameraScreen.Visibility = Visibility.Hidden;
            }

            if (CrosshairsCheckBox.IsChecked ?? false)
            {
                XCrosshairHomeScreen.Visibility = Visibility.Visible;
                XCrosshairCameraScreen.Visibility = Visibility.Visible;
                YCrosshairHomeScreen.Visibility = Visibility.Visible;
                YCrosshairCameraScreen.Visibility = Visibility.Visible;
            }
            else
            {
                XCrosshairHomeScreen.Visibility = Visibility.Hidden;
                XCrosshairCameraScreen.Visibility = Visibility.Hidden;
                YCrosshairHomeScreen.Visibility = Visibility.Hidden;
                YCrosshairCameraScreen.Visibility = Visibility.Hidden;
            }
        }


        #endregion



        #region Collection Screen Event Handlers
        // ================================================================== COLLECTION SCREEN EVENT HANDLERS ================================================================== //


        private bool IsCollectionChanged;   // boolean to detect if any changes were made to a collection in progress


        /// <summary>
        /// Event handler that updates collection settings
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollectionListView.SelectedItem != null)
            {
                // verifies whether a user wants to discard progress on currently displayed collection
                if (IsCollectionChanged == true)
                {
                    MessageBoxResult result = MessageBox.Show("This will discard the changes you've made to the current collection. \nAre you sure you want to proceed?", "Warning!",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                        return;
                    else
                        IsCollectionChanged = false;
                }

                // enables the save collection button
                SaveCollectionButton.IsEnabled = true;

                // clears current poses list
                if (PoseList != null)
                    PoseList.Clear();
                CollectionScreenPoseListView.Items.Clear();

                // retrieves the information from the collection config file
                Collection collection = CollectionList.ElementAt(CollectionListView.SelectedIndex);

                // inserts collection information
                SaveDirectoryTextBox.Text = collection.savingDirectory;
                CollectionNrTextBox.Text = collection.collectionNumber;
                CollectionDeviceTextBox.Text = collection.deviceName;
                CollectionModalityTextBox.Text = collection.modality;
                CollectionNameTextBox.Text = collection.name;

                // makes a copy list for collections
                PoseList = new List<Pose>();
                foreach (Pose pose in collection.poses)
                    PoseList.Add(pose);

                // refreshes the list of poses in the CollectionScreenPoseListView
                LoadCollectionPoseList();
            }
        }


        /// <summary>
        /// Event handler that deletes the selected collection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            // if a collection is selected, delete the collection
            if (CollectionListView.SelectedItem != null)
            {
                // verifies whether a user wants to delete the selected collection
                MessageBoxResult result = MessageBox.Show("This will delete the selected collection permanently. Are you sure you want to delete it?", "Warning!",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;

                // makes a copy list for collections
                List<Collection> newList = new List<Collection>();
                foreach (Collection collection in CollectionList)
                    newList.Add(collection);

                // removes the collection
                newList.Remove(CollectionList.ElementAt(CollectionListView.SelectedIndex));

                // replaces the original CollectionList with the newList
                CollectionList = newList.OrderBy(col => col.collectionNumber).ThenBy(col => col.name).ToList();

                // converts list back to JSON, writes to configuration file, and refreshes collections list
                string output = JsonConvert.SerializeObject(CollectionList.ToArray(), Formatting.Indented);
                File.WriteAllText(COLLECTIONCONFIGFILE, output);
                LoadCollections();
            }
            // if a collection is not selected, throw an error
            else
                ShowMessage("red", "No collection selected", "Please select a collection to delete first.");
        }


        /// <summary>
        /// Event handler that opens folder browser selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrowseSaveDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            // deselects any selected collection in the CollectionListView
            CollectionListView.SelectedItem = null;

            // opens the folder browser dialog to select a folder
            string saveDirectory;
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                // displays the selected directory
                try
                {
                    dialog.ShowDialog();
                    saveDirectory = dialog.SelectedPath;
                    SaveDirectoryTextBox.Text = saveDirectory;
                    IsCollectionChanged = true;
                }
                // if the specified directory does not exist, throw an error
                catch
                { ShowMessage("red", "Error", "Directory does not exist."); }
            }
        }


        /// <summary>
        /// Event handler that deselects selected collection when user types in CollectionNrTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionNrTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that deselects selected collection when user types in CollectionDeviceTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionDeviceTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that deselects selected collection when user types in CollectionModalityTextBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionModalityTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that removes a pose from the CollectionScreenPoseListView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemovePoseButton_Click(object sender, RoutedEventArgs e)
        {
            // verifies that number of poses is nonnegative
            if (PoseList == null || PoseList.Count <= 0)
                return;

            // disables the SaveCollectionButton
            if (PoseList.Count == 1)
            {
                SaveCollectionButton.IsEnabled = false;
                IsCollectionChanged = false;
            }
            else
                IsCollectionChanged = true;

            // removes the last pose
            PoseList.RemoveAt(PoseList.Count - 1);

            // display the new list of poses
            LoadCollectionPoseList();

            // deselects any selected collection in the CollectionListView
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that adds a pose from the CollectionScreenPoseListView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddPoseButton_Click(object sender, RoutedEventArgs e)
        {
            // initializes the PoseList if it is null
            if (PoseList == null)
                PoseList = new List<Pose>();

            // verifies that number of poses is within reason
            if (PoseList.Count >= 100)
            {
                ShowMessage("red", "Error", "Number of poses is too large.");
                return;
            }

            // default thumbnail path
            string defaultThumb = System.IO.Path.GetFullPath(@"Resources\Thumbnails\FACE\RAW\0.png");

            // adds default pose
            Pose defaultPose = new Pose("Sample Title", "Sample Description", defaultThumb, "sample_filename.JPEG", null);
            PoseList.Add(defaultPose);

            // display the new list of poses
            LoadCollectionPoseList();

            // adjusts GUI
            SaveCollectionButton.IsEnabled = true;
            CollectionListView.SelectedItem = null;
            IsCollectionChanged = true;
        }


        /// <summary>
        /// Event handler that deselects selected collection when CollectionNameTextBox is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionNameTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that deselects selected collection when CollectionScreenPoseListView is pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionScreenPoseListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CollectionListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that opens the PoseSettings grid when a pose is selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionScreenPoseListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CollectionScreenPoseListView.SelectedItem != null)
            {
                Pose pose = PoseList.ElementAt(CollectionScreenPoseListView.SelectedIndex);

                // makes pose settings visible
                PoseSettingsGridBackground.Visibility = Visibility.Visible;
                PoseSettingsGrid.Visibility = Visibility.Visible;

                // selects the camera profile profile of the selected pose
                CameraProfile item = CameraProfileList.FirstOrDefault(profile => pose.cameraProfile == profile.name);
                if (item != null)
                    PoseCameraProfileComboBox.SelectedIndex = CameraProfileList.IndexOf(item);


                // loads a thumbnail if the pose has one
                if (pose.thumbnail != null)
                {
                    PoseThumbnailLabel.Content = pose.thumbnail;
                    PoseThumbnailImage.Source = LoadImage(pose.thumbnail, 45);
                    PoseThumbnailImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                    PoseThumbnailImage.RenderTransform = new RotateTransform(90);
                }

                // fills in the rest of the pose data
                PoseNoLabel.Content = CollectionScreenPoseListView.SelectedIndex + 1;
                PoseTitleTextBox.Text = pose.title;
                PoseDescTextBox.Text = pose.description;
                string[] filename = pose.filename.Split('.');
                PoseFilenameTextbox.Text = filename[0];
                PoseFilenameComboBox.Text = "." + filename[1];
            }
        }


        /// <summary>
        /// Event handler that disables ListView navigation through keyboard up key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionScreenPoseListView_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }


        /// <summary>
        /// Event handler that disables ListView navigation through keyboard up key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CollectionScreenPoseListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }


        /// <summary>
        /// Event handler that hides the PoseSettings grid and deselects any selected pose
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HidePoseSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // verifies that a camera profile has been chosen before exiting
            if (PoseList.ElementAt(CollectionScreenPoseListView.SelectedIndex).cameraProfile == null)
            {
                MessageBoxResult result = MessageBox.Show(
                    "There is no selected camera profile for this pose. This may cause problems during a session. Are you sure you want to exit pose settings?", "Warning!",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;
            }

            PoseSettingsGridBackground.Visibility = Visibility.Collapsed;
            PoseSettingsGrid.Visibility = Visibility.Collapsed;
            CollectionScreenPoseListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that opens dialog box to choose thumbnail
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            // gets the full directory of the thumbnails folder
            string thumbnailDirectory = System.IO.Path.GetFullPath(@"Resources\Thumbnails");

            // create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.InitialDirectory = thumbnailDirectory;

            // set filter for file extension and default file extension 
            dlg.DefaultExt = ".png";
            dlg.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";

            // display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // get the selected file name
            string filename;
            if (result == true)
                // Open document 
                filename = dlg.FileName;
            else
                return;

            // displays the filename of the thumbnail chosen
            PoseThumbnailLabel.Content = filename;

            // loads the thumbnail and rotates the image to fit
            PoseThumbnailImage.Source = LoadImage(filename, 150);
            PoseThumbnailImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            PoseThumbnailImage.RenderTransform = new RotateTransform(90);
        }


        /// <summary>
        /// Event handler that applies changes to the selected pose
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PoseSettings_ApplyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // checks that all required fields are filled (title, description, thumbnail, camera profile, filename)
            if (PoseTitleTextBox.Text.Length <= 0 ||
                PoseDescTextBox.Text.Length <= 0 ||
                PoseThumbnailLabel.Content.ToString().Length <= 0 ||
                PoseFilenameTextbox.Text.Length <= 0 ||
                PoseCameraProfileComboBox.SelectedItem == null)
            {
                ShowMessage("red", "Unfilled fields", "Title, description, thumbnail, camera profile, and filename must be filled before saving pose.");
                return;
            }

            // regex to identify special characters
            var regex = new Regex(@"[.,/;'\[\]\\`<>?:\""{}|~!@#$%^&*()+= ]+");

            // checks if filename is valid
            if (regex.IsMatch(PoseFilenameTextbox.Text))
            {
                ShowMessage("red", "Invalid filename", "Cannot use spaces or special characters.");
                return;
            }

            // edits the pose with the right changes
            Pose pose = new Pose(
                PoseTitleTextBox.Text,
                PoseDescTextBox.Text,
                PoseThumbnailLabel.Content.ToString(),
                PoseFilenameTextbox.Text + PoseFilenameComboBox.Text,
                CameraProfileList[PoseCameraProfileComboBox.SelectedIndex].name);
            PoseList[CollectionScreenPoseListView.SelectedIndex] = pose;

            // refreshes the pose list
            LoadCollectionPoseList();
            IsCollectionChanged = true;

            // adjusts GUI
            PoseSettingsGrid.Visibility = Visibility.Collapsed;
            PoseSettingsGridBackground.Visibility = Visibility.Collapsed;
            CollectionScreenPoseListView.SelectedItem = null;
        }


        /// <summary>
        /// Event handler that saves the collection settings into a new collection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveCollectionButton_Click(object sender, RoutedEventArgs e)
        {
            string collectionName = CollectionNameTextBox.Text; // the new collection name
            string saveDirectory = SaveDirectoryTextBox.Text;   // the new collection save directory
            string collectionNr = CollectionNrTextBox.Text;     // the new collection number
            string deviceName = CollectionDeviceTextBox.Text;   // the new collection device
            string modality = CollectionDeviceTextBox.Text;     // the new collection modality

            // stops the user if a session is ongoing
            if(IsSessionOngoing)
            {
                ShowMessage("red", "Session is ongoing", "You cannot save a collection during a session.");
                return;
            }

            // regex to identify special characters
            var regex = new Regex(@"[.,/;'\[\]\\`<>?:\""{}|~!@#$%^&*()+= ]+");

            // checks if collection name is valid
            if (regex.IsMatch(collectionName))
            {
                ShowMessage("red", "Invalid collection name", "Cannot use spaces or special characters.");
                return;
            }

            // checks if device name is valid
            if (regex.IsMatch(deviceName))
            {
                ShowMessage("red", "Invalid device name", "Cannot use spaces or special characters.");
                return;
            }

            // checks if modality is valid
            if (regex.IsMatch(modality))
            {
                ShowMessage("red", "Invalid modality", "Cannot use spaces or special characters.");
                return;
            }

            // checks that all required fields are filled (save directory, collection number, device name, collection name)
            if (saveDirectory.Length <= 0 ||
                collectionNr.Length <= 0 ||
                deviceName.Length <= 0 ||
                modality.Length <= 0 ||
                collectionName.Length <= 0)
            {
                ShowMessage("red", "Unfilled fields", "Save directory, collection number, device name, and collection name must be filled before saving collection.");
                return;
            }

            // checks if the collection number entered is an integer
            int newColNrInt;
            if (!int.TryParse(CollectionNrTextBox.Text, out newColNrInt))
            {
                ShowMessage("red", "Error", "\"Collection #\" accepts only integers.");
                return;
            }

            // checks if an accurate camera name is attached to the collection
            if (CameraComboBox.SelectedIndex == 0)
            {
                ShowMessage("red", "Camera must be connected", "A camera must be connected before saving a collection.");
                return;
            }

            // verifies that the camera connected is the correct camera to be associated with the collection
            else
            {
                MessageBoxResult result = MessageBox.Show(
                    "Are you sure that " + CameraComboBox.Text + " is the camera you want associated with this collection?", "Check camera",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                    return;
            }

            // creates a brand new collection and enters the specified data
            Collection collection = new Collection(
                collectionName,
                collectionNr,
                PoseList.Count,
                saveDirectory,
                deviceName,
                modality, 
                CameraComboBox.Text);
            collection.poses = PoseList;

            // makes a copy list for collections
            List<Collection> newList = new List<Collection>();
            foreach (Collection col in CollectionList)
                newList.Add(col);

            // checks for repeat collections
            foreach (Collection col in CollectionList)
            {
                // verifies whether user wants to replace an existing collection with the same collection number
                int colNrInt;
                int.TryParse(col.collectionNumber, out colNrInt);
                if (colNrInt == newColNrInt)
                {
                    MessageBoxResult result = MessageBox.Show("A collection with this collection number already exists. Do you want to overwrite it?", "Warning!",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        newList.Remove(col);
                        break;
                    }
                    else
                        return;
                }
            }

            // adds the collection to the list if the collection hasn't already been overwritten
            newList.Add(collection);

            // orders the new collection list and assigns it to the original
            CollectionList = newList.OrderBy(col => col.collectionNumber).ThenBy(col => col.name).ToList();

            // writes the collection to the configuration file
            string output = JsonConvert.SerializeObject(CollectionList, Formatting.Indented);
            File.WriteAllText(COLLECTIONCONFIGFILE, output);
            LoadCollections();

            // clears the list of poses
            IsCollectionChanged = false;
            PoseList = null;
            CollectionScreenPoseListView.Items.Clear();

            // adjusts the UI
            SaveCollectionButton.IsEnabled = false;
            CollectionListView.SelectedItem = null;
            SaveDirectoryTextBox.Text = null;
            CollectionNrTextBox.Text = null;
            CollectionDeviceTextBox.Text = null;
            CollectionNameTextBox.Text = null;
        }


        #endregion
    }
}

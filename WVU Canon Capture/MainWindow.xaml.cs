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
            DisplaySplashScreen();
            InitializeComponent();

            // initializes the API and retrieves the list of connected cameras
            API = new CanonAPI();
            InitializeCameraList();
            InitializeCameraProfiles();
            InitializeCollectionProfiles();

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
        private void InitializeCameraList()
        {

            // retrieves all Canon cameras connected to the computer
            API.CameraAdded += API_CameraAdded;
            CameraList = API.GetCameraList();

            // populates the Camera ComboBox with the list
            foreach (Camera cameraOption in CameraList)
                CameraComboBox.Items.Add(cameraOption.DeviceName);

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
                MainCamera.StartLiveView();
                ToggleLiveViewHomeButton.Content = "\u23f8";

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
                    if (ToggleLiveViewHomeButton != null)
                        ToggleLiveViewHomeButton.Content = "\u25b6";

                    // closes the camera session
                    MainCamera?.StopLiveView();
                    MainCamera?.CloseSession();
                }
                catch (Exception ex) { }
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
        private void SetCameraSettings(String fstop, String exposure, String iso, String wb)
        {

        }


        /// <summary>
        /// Event handler that detects when a new camera has been connected
        /// </summary>
        /// <param name="sender"></param>
        private void API_CameraAdded(CanonAPI sender)
        {
            if (CameraComboBox.SelectedIndex != 0)
                SetLiveViewOn(true);
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


        #endregion



        #region Camera Profiles
        // ================================================================== CAMERA PROFILES ================================================================== //


        const String CAMERACONFIGFILE = @"camera_profiles.json";    // the filename of the camera configuration file
        List<CameraProfile> CameraProfileList;                      // a list of CameraProfiles


        /// <summary>
        /// Retrieves the list of camera profiles
        /// </summary>
        private void InitializeCameraProfiles()
        {
            using (StreamReader r = new StreamReader(CAMERACONFIGFILE))
            {
                string json = r.ReadToEnd();
                CameraProfileList = JsonConvert.DeserializeObject<List<CameraProfile>>(json);

                // add each camera profile to the camera profiles combobox
                foreach (CameraProfile profile in CameraProfileList)
                {
                    // Adds the profile to the CameraProfilesListView
                    // the profile name
                    Label proName = new Label()
                    {
                        Content = profile.name,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255)),
                        FontSize = 12,
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

            public String name { get; set; }           // the camera profile name
            public String fstop { get; set; }          // the F-stop setting
            public String exposure { get; set; }       // the exposure (shutter-speed) setting
            public String iso { get; set; }            // the ISO setting
            public String whiteBalance { get; set; }   // the white balance setting


            /// <summary>
            /// Default constructor for a camera profile
            /// </summary>
            public CameraProfile()
            {
                name = null;
                fstop = null;
                exposure = null;
                iso = null;
                whiteBalance = null;
            }

            
            /// <summary>
            /// Constructor for a camera profile
            /// </summary>
            /// <param name="nameVal">the camera profile name</param>
            /// <param name="fstopVal">the F-stop setting</param>
            /// <param name="expoVal">the exposure (shutter-speed) setting</param>
            /// <param name="isoVal">the ISO setting</param>
            /// <param name="wbVal">the white balance setting</param>
            public CameraProfile(String nameVal, String fstopVal, String expoVal, String isoVal, String wbVal)
            {
                name = nameVal;
                fstop = fstopVal;
                exposure = expoVal;
                iso = isoVal;
                whiteBalance = wbVal;
            }

        }


        #endregion



        #region Collection Profiles
        // ================================================================== COLLECTION PROFILES ================================================================== //

        
        const String COLLECTIONCONFIGFILE = @"collection_profiles.json";    // the filename of the camera configuration file
        List<CollectionProfile> CollectionProfileList;                          // a list of CollectionProfiles


        /// <summary>
        /// Retrieves the list of camera profiles
        /// </summary>
        private void InitializeCollectionProfiles()
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
            public String name { get; set; }                // the collection profile name
            public String collectionNumber { get; set; }    // the collection number
            public int numberOfPoses { get; set; }          // the number of poses in the collection
            public String savingDirectory { get; set; }     // the save path of the collection
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
            public CollectionProfile(String nameVal, String colNum, int nrPoses, String savePath)
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
            String title { get; set; }                  // the pose title
            String description { get; set; }            // the description of the pose
            String thumbnail { get; set; }              // the thumbnail for the pose
            String device { get; set; }                 // the device name for the pose
            String filename { get; set; }               // the filename for the pose
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
            public Pose(String titleVal, String descVal, String thumbSource, String devVal, String filnamVal, CameraProfile camProfile)
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

        
        /// <summary>
        /// Switches between window screens
        /// </summary>
        /// <param name="screen"></param>
        private void SwitchScreen(String screen)
        {

            var bc = new BrushConverter();
            if (screen == "home")
            {
                // switch screens
                HomeScreenGrid.Visibility = Visibility.Visible;
                CameraScreenGrid.Visibility = Visibility.Collapsed;
                CollectionsScreenGrid.Visibility = Visibility.Collapsed;

                // button highlighting
                TopToolBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#2C2A29");
                HomeNavigationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#FF3F4A52");
                CameraNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
                CollectionsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;

                // button fontcolors
                HomeNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                HomeNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CameraNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CameraNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CollectionsNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CollectionsNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                SettingsMenu.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
            }
            else if (screen == "camera")
            {
                // switch screens
                HomeScreenGrid.Visibility = Visibility.Collapsed;
                CameraScreenGrid.Visibility = Visibility.Visible;
                CollectionsScreenGrid.Visibility = Visibility.Collapsed;

                // button highlighting
                TopToolBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#802115");
                HomeNavigationButton.Background = System.Windows.Media.Brushes.Transparent ;
                CameraNavigationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#AA4639");
                CollectionsNavigationButton.Background = System.Windows.Media.Brushes.Transparent;

                // button fontcolors
                HomeNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                HomeNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CameraNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CameraNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CollectionsNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CollectionsNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                SettingsMenu.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
            }
            else if (screen == "collections")
            {
                // switch screens
                HomeScreenGrid.Visibility = Visibility.Collapsed;
                CameraScreenGrid.Visibility = Visibility.Collapsed;
                CollectionsScreenGrid.Visibility = Visibility.Visible;

                // button highlighting
                TopToolBar.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                HomeNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
                CameraNavigationButton.Background = System.Windows.Media.Brushes.Transparent;
                CollectionsNavigationButton.Background = (System.Windows.Media.Brush)bc.ConvertFrom("#333F48");

                // button fontcolors
                HomeNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#0000FF");
                HomeNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#0000FF");
                CameraNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#0000FF");
                CameraNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#0000FF");
                CollectionsNavigationButtonIcon.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                CollectionsNavigationButtonLabel.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#FFFFFF");
                SettingsMenu.Foreground = (System.Windows.Media.Brush)bc.ConvertFrom("#0000FF");
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
        private void ShowMessage(String color, String title, String message)
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
        private void SaveFile(String dir, String filename)
        {
            String filepath = dir + "\\" + filename;
            // TODO: implement saving to filepath
        }


        /// <summary>
        /// Populates the PoseListView with collection thumbnails
        /// </summary>
        private void PopulatePoseListView()
        {
            // TODO: IMPLEMENT POPULATING THE POSELISTVIEW
            String thumbnailSource = null;
            LoadImage(thumbnailSource, LOWRESOLUTION);
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
        private BitmapImage LoadImage(String source, int imageHeight)
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
            }
            else
            {
                if(ToggleLiveViewHomeButton != null)
                    ToggleLiveViewHomeButton.Visibility = Visibility.Collapsed;
                SetLiveViewOn(false);
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
            String input = RIDTextBox.Text.ToLower();
            String[] ridDateCol = input.Split('_');
            String rid;
            String date;
            String col;

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






        #endregion



        #region Camera Screen Event Handlers
        // ================================================================== CAMERA SCREEN EVENT HANDLERS ================================================================== //

        #endregion



        #region Collection Screen Event Handlers
        // ================================================================== COLLECTION SCREEN EVENT HANDLERS ================================================================== //

        #endregion



    }
}

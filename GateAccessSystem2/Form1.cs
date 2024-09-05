using System;
using System.Drawing;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using MaterialSkin;
using MaterialSkin.Controls;
using AForge.Video;
using Emgu.CV;
using Emgu.CV.Structure;
using Tesseract;
using Emgu.CV.CvEnum;
using System.Threading.Tasks;
using System.Net;
using Python.Runtime;
using System.Text.RegularExpressions;

namespace GateAccessSystem2
{
    public partial class Form1 : MaterialForm
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private TesseractEngine ocrEngine;

       
        public Form1()
        {
            InitializeComponent();
            InitializeOCR();
            

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;
            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey800,
                Primary.BlueGrey900,
                Primary.BlueGrey500,
                Accent.LightBlue700,
                TextShade.WHITE
            );

            Label label1 = new Label();
            label1.Location = new Point(20, 20);
            tabPage1.Controls.Add(label1);

            Label label2 = new Label();
            label2.Location = new Point(20, 20);
            tabPage2.Controls.Add(label2);


        }


        private void InitializeOCR()
        {
            try
            {
                string tessDataPath = @"C:\Users\judde\source\repos\GateAccessSystem2\GateAccessSystem2\GateAccessSystem2\tessdata";
                ocrEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Tesseract OCR Engine: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }


        private void InitializeCamera()
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                {
                    MessageBox.Show("No webcam found!");
                    return;
                }

                videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
                videoSource.NewFrame += videoSource_NewFrame;
                videoSource.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing camera: {ex.Message}");
            }
        }

        private void videoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                Bitmap frame = (Bitmap)eventArgs.Frame.Clone();

                if (DL_pictureBox.InvokeRequired)
                {
                    DL_pictureBox.Invoke(new Action(() => DL_pictureBox.Image = frame));
                }
                else
                {
                    DL_pictureBox.Image = frame;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during video frame processing: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }


        private void ProcessFrameForOCR(Bitmap frame)
        {
            try
            {
                using (var img = frame.ToImage<Bgr, byte>())
                {
                    var gray = img.Convert<Gray, byte>();
                    var threshold = gray.ThresholdBinary(new Gray(100), new Gray(255));

                    using (var pix = PixConverter.ToPix(threshold.ToBitmap()))
                    {
                        using (var result = ocrEngine.Process(pix))
                        {
                            string text = result.GetText().Trim();

                            if (!string.IsNullOrEmpty(text))
                            {
                                DL_materialTextbox.Invoke(new Action(() => DL_materialTextbox.Text = text));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during OCR processing: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCamera();

            if (ocrEngine != null)
            {
                ocrEngine.Dispose();
            }

            base.OnFormClosing(e);
        }

        private void materialSwitch1_CheckedChanged(object sender, EventArgs e)
        {
            if (materialSwitch1.Checked)
            {
                P1_pictureBox1.Visible = true;
                P1_pictureBox2.Visible = false;
                materialLabel1.Visible = true;
                materialLabel1.Text = "RFID Tag is Verified!";
                materialLabel5.Visible = false;
            }
            else
            {
                P1_pictureBox1.Visible = false;
                P1_pictureBox2.Visible = true;
                materialLabel1.Visible = true;
                materialLabel1.Text = "RFID Tag is not Verified!";
                materialLabel5.Visible = true;
            }
        }

        private void StartCamera()
        {
            try
            {
                if (videoSource == null || !videoSource.IsRunning)
                {
                    videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
                    videoSource.NewFrame += new NewFrameEventHandler(videoSource_NewFrame);
                    videoSource.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting camera: {ex.Message}");
            }
        }

        private async void StopCamera()
        {
            try
            {
                if (videoSource != null && videoSource.IsRunning)
                {
                    videoSource.SignalToStop();
                    await Task.Run(() => videoSource.WaitForStop());
                    videoSource.NewFrame -= videoSource_NewFrame;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping the camera: {ex.Message}");
            }
        }


        private void materialSwitch2_CheckedChanged(object sender, EventArgs e)
        {
            if (materialSwitch2.Checked)
            {
                DL_pictureBox.Visible = true;
                if (videoDevices == null)
                {
                    try
                    {
                        videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                        if (videoDevices.Count == 0)
                        {
                            MessageBox.Show("No webcam found!");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error initializing video devices: {ex.Message}");
                        return;
                    }
                }

                StartCamera(); // Start the camera when the switch is toggled on
            }
            else
            {
                StopCamera(); // Stop the camera when the switch is toggled off
                DL_pictureBox.Visible = false;
            }
        }

        

        private void btn_browse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                openFileDialog.Title = "Select an Image";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    LP_pictureBox.Image = Image.FromFile(filePath);
                    LP_pictureBox.Tag = filePath;
                }
            }
        }

        private void btn_extract_Click(object sender, EventArgs e)
        {
            try
            {
                if (LP_pictureBox.Image != null)
                {
                    Bitmap bitmap = new Bitmap(LP_pictureBox.Image);

                    // Perform OCR on the image
                    string rawText = PerformOCR(bitmap);

                    // Clean the OCR output by removing unnecessary special characters
                    rawText = CleanText(rawText);

                    // Extract specific fields from the text
                    var licenseInfo = ExtractLicenseInfo(rawText);

                    // Update the text boxes with the extracted information
                    UpdateTextBoxes(licenseInfo);

                    MessageBox.Show(rawText);
                }
                else
                {
                    MessageBox.Show("No image found in the PictureBox. Please select an image first.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during OCR processing: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }
        private void UpdateTextBoxes(LicenseInfo licenseInfo)
        {
            // Update each text box with corresponding data
            foreach (Control ctrl in Controls)
            {
                if (ctrl is MaterialTextBox textBox)
                {
                    int index = int.Parse(textBox.Name.Replace("materialTextBox", ""));
                    switch (index)
                    {
                        case 0:
                            textBox.Text = licenseInfo.LastName;
                            break;
                        case 1:
                            textBox.Text = licenseInfo.FirstName;
                            break;
                        case 2:
                            textBox.Text = licenseInfo.MiddleName;
                            break;
                        case 3:
                            textBox.Text = licenseInfo.Nationality;
                            break;
                        case 4:
                            textBox.Text = licenseInfo.Sex;
                            break;
                        case 5:
                            textBox.Text = licenseInfo.DateOfBirth;
                            break;
                        case 6:
                            textBox.Text = licenseInfo.Weight;
                            break;
                        case 7:
                            textBox.Text = licenseInfo.Height;
                            break;
                        case 8:
                            textBox.Text = licenseInfo.Address;
                            break;
                        case 9:
                            textBox.Text = licenseInfo.LicenseNo;
                            break;
                        case 10:
                            textBox.Text = licenseInfo.ExpirationDate;
                            break;
                        case 11:
                            textBox.Text = licenseInfo.AgencyCode;
                            break;
                        case 12:
                            textBox.Text = licenseInfo.BloodType;
                            break;
                        case 13:
                            textBox.Text = licenseInfo.EyeColor;
                            break;
                        case 14:
                            textBox.Text = licenseInfo.Restrictions;
                            break;
                        case 15:
                            textBox.Text = licenseInfo.Conditions;
                            break;
                    }
                }
            }
        }
        private string CleanText(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, @"[^a-zA-Z0-9\s:/,-]", string.Empty);
        }
        //hereeeeeeeeeeeeeeeeeee
        private LicenseInfo ExtractLicenseInfo(string text)
        {
            LicenseInfo info = new LicenseInfo();

            // Use regular expressions or string manipulation to extract specific fields
            info.LastName = ExtractField(text, @"Last Name:\s*(\w+)");
            info.FirstName = ExtractField(text, @"First Name:\s*(\w+)");
            info.MiddleName = ExtractField(text, @"Middle Name:\s*(\w+)");
            info.Nationality = ExtractField(text, @"Nationality:\s*(\w+)");
            info.Sex = ExtractField(text, @"Sex:\s*(M|F)");
            info.DateOfBirth = ExtractField(text, @"Date of Birth:\s*(\d{4}/\d{2}/\d{2})");
            info.Weight = ExtractField(text, @"Weight:\s*(\d+)");
            info.Height = ExtractField(text, @"Height:\s*(\d+)");
            info.Address = ExtractField(text, @"Address:\s*(.+)");
            info.LicenseNo = ExtractField(text, @"License No.:\s*(\w+)");
            info.ExpirationDate = ExtractField(text, @"Expiration Date:\s*(\d{4}/\d{2}/\d{2})");
            info.AgencyCode = ExtractField(text, @"Agency Code:\s*(\w+)");
            info.BloodType = ExtractField(text, @"Blood Type:\s*(\w+)");
            info.EyeColor = ExtractField(text, @"Eye Color:\s*(\w+)");
            info.Restrictions = ExtractField(text, @"Restrictions:\s*(.+)");
            info.Conditions = ExtractField(text, @"Conditions:\s*(.+)");

            return info;
        }

        // Helper method to extract specific fields using regex
        private string ExtractField(string text, string pattern)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        // Class to hold the driver's license information
        class LicenseInfo
        {
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string Nationality { get; set; }
            public string Sex { get; set; }
            public string DateOfBirth { get; set; }
            public string Weight { get; set; }
            public string Height { get; set; }
            public string Address { get; set; }
            public string LicenseNo { get; set; }
            public string ExpirationDate { get; set; }
            public string AgencyCode { get; set; }
            public string BloodType { get; set; }
            public string EyeColor { get; set; }
            public string Restrictions { get; set; }
            public string Conditions { get; set; }
        }

        private string PerformOCR(Bitmap bitmap)
        {
            using (var img = bitmap.ToImage<Bgr, byte>())
            {
                var gray = img.Convert<Gray, byte>();
                var threshold = gray.ThresholdBinary(new Gray(100), new Gray(255));

                using (var pix = PixConverter.ToPix(threshold.ToBitmap()))
                {
                    using (var result = ocrEngine.Process(pix))
                    {
                        return result.GetText().Trim();
                    }
                }
            }
        }
        
        private void RunYOLODetection(string imagePath)
        {
            try
            {
                using (Py.GIL())
                {
                    dynamic yoloScript = Py.Import("yolo_license_plate_detection");
                    dynamic results = yoloScript.detect_license_plate(imagePath);

                    // Process and display the results as needed
                    MessageBox.Show(results.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during YOLO detection: {ex.Message}");
            }
        }

        private async void btnCapture_Click(object sender, EventArgs e)
        {
            try
            {
                if (DL_pictureBox.Image != null)
                {
                    // Capture the current frame displayed in DL_pictureBox
                    Bitmap capturedFrame = new Bitmap(DL_pictureBox.Image);

                    // Display the captured frame in pictureBoxFrame
                    pictureBoxFrame.Image = new Bitmap(capturedFrame);

                    // Process the frame for OCR
                    ProcessFrameForOCR(capturedFrame);
                }
                else
                {
                    MessageBox.Show("No image found. Please make sure the camera is on.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during capture and OCR processing: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }
    }
}



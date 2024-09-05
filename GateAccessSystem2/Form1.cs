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
using System.Text;

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
                    using (var img = bitmap.ToImage<Bgr, byte>())
                    {
                        var gray = img.Convert<Gray, byte>();
                        var threshold = gray.ThresholdBinary(new Gray(100), new Gray(255));

                        using (var pix = PixConverter.ToPix(threshold.ToBitmap()))
                        {
                            using (var result = ocrEngine.Process(pix))
                            {
                                string text = result.GetText().Trim();

                                // Display the full text in the multi-line text box
                                MessageBox.Show(text, "Extracted Text");

                                // Clean and process the text
                                CleanAndProcessText(text);
                            }
                        }
                    }
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
        private void CleanAndProcessText(string rawText)
        {
            try
            {
                // Define the field names and headings to be removed
                var headings = new[]
                {
            "REPUBLIC OF THE PHILIPPINES", "DEPARTMENT OF TRANSPORTATION",
            "LAND TRANSPORTATION OFFICE", "NON-PROFESSIONAL DRIVER'S LICENSE",
            "Last Hame. First Name. Middle Name.", "Nationality", "Sex", "Date of Sith",
            "Weight (kg)", "Height)", "Addrass", "ene fo Expiration Date",
            "Agency Code", "Blood lype", "Eyes Color", "COTE", "Restgctions", "Conditions"
        };

                // Remove headings and labels from the text
                string cleanedText = rawText;
                foreach (var heading in headings)
                {
                    cleanedText = cleanedText.Replace(heading, string.Empty);
                }

                // Optionally remove extra spaces and new lines
                cleanedText = System.Text.RegularExpressions.Regex.Replace(cleanedText, @"\s+", " ").Trim();

                // Define the regular expressions for each field
                var fieldRegex = new[]
                {
            new { Pattern = @"([A-Za-z\s]+)", Field = materialTextBox1 }, // Last Name
            new { Pattern = @"([A-Za-z\s]+)", Field = materialTextBox2 }, // First Name
            new { Pattern = @"([A-Za-z\s]+)\s([A-Za-z\s]+)\s([A-Za-z\s]+)", Field = materialTextBox3 }, // Middle Name
            new { Pattern = @"([A-Z]{3})", Field = materialTextBox4 }, // Nationality
            new { Pattern = @"([M|F])", Field = materialTextBox5 }, // Sex
            new { Pattern = @"(\d{4}/\d{2}/\d{2})", Field = materialTextBox6 }, // Date of Birth
            new { Pattern = @"(\d{2})", Field = materialTextBox7 }, // Weight
            new { Pattern = @"(\d{3})", Field = materialTextBox8 }, // Height
            new { Pattern = @"(UNIT/HOUSE NO.\sBUILDING,\sSTREET NAME,\sBARANGAY,\sCITY/MUNICIPALITY)", Field = materialTextBox9 }, // Address
            new { Pattern = @"(NO\d-\d{2}-\d{6})", Field = materialTextBox10 }, // License No.
            new { Pattern = @"(\d{4}/\d{2}/\d{2})", Field = materialTextBox11 }, // Expiration Date
            new { Pattern = @"(N\d{2})", Field = materialTextBox12 }, // Agency Code
            new { Pattern = @"([A-Z])", Field = materialTextBox13 }, // Blood Type
            new { Pattern = @"([A-Z]+)", Field = materialTextBox14 }, // Eye Color
            new { Pattern = @"(\d{2})", Field = materialTextBox15 }, // Restrictions
            new { Pattern = @"(NONE)", Field = materialTextBox16 } // Conditions
        };

                // Extract the values for each field using regular expressions
                foreach (var field in fieldRegex)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanedText, field.Pattern);
                    if (match.Success)
                    {
                        if (field.Field == materialTextBox2)
                        {
                            field.Field.Text = match.Groups[1].Value.Trim() + " " + match.Groups[2].Value.Trim();
                        }
                        else if (field.Field == materialTextBox3)
                        {
                            field.Field.Text = match.Groups[3].Value.Trim();
                        }
                        else
                        {
                            field.Field.Text = match.Groups[1].Value.Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing cleaned text: {ex.Message}");
            }
        }
       
     

        // Helper method to extract specific fields using regex

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



using System;
using System.Drawing;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using MaterialSkin;
using MaterialSkin.Controls;
using AForge.Video;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;
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
        private SerialPort serialPort;
        private bool isRfidOn = false;
        private string connectionString = "server=localhost;database=thesis;user=root;password=parasathesis;";
        private FilterInfoCollection videoDevices;
        private string connectionString = "server=localhost;database=thesis;user=root;password=parasathesis;";
        private VideoCaptureDevice videoSource;
        private TesseractEngine ocrEngine;
        private SerialPort rfidReader;
        



        public Form1()
        {
            InitializeComponent();
            InitializeOCR();

            // Initialize RFID reader (replace COM3 with your actual port)
            rfidReader = new SerialPort("COM4", 9600, Parity.None, 8, StopBits.One);
            rfidReader.DataReceived += new SerialDataReceivedEventHandler(RFID_DataReceived);
            rfidReader.Open();

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

            // Setup Timer for RFID detection
            timerRfid = new Timer();
            timerRfid.Interval = 5000; // 5 seconds (5000 milliseconds)
            timerRfid.Tick += timerRfid_Tick;

            Label label1 = new Label();
            label1.Location = new Point(20, 20);
            tabPage1.Controls.Add(label1);

            Label label2 = new Label();
            label2.Location = new Point(20, 20);
            tabPage2.Controls.Add(label2);

            

            // Initialize the SerialPort with the RFID reader's port settings
            serialPort = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One);
            serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);

            // Initialize PictureBox visibility
            P1_pictureBox1.Visible = false;
            P1_pictureBox2.Visible = false;


        }

        private void RFID_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string rfidData = rfidReader.ReadExisting();
                if (!string.IsNullOrEmpty(rfidData))
                {
                    // Trigger this on the main UI thread
                    this.Invoke((MethodInvoker)delegate
                    {
                        // Stop any existing timer when RFID is detected
                        timerRfid.Stop();

                        // Start the 5-second timer for verification check
                        timerRfid.Start();

                        // Update UI for detected RFID
                        P1_pictureBox1.Visible = true;
                        P1_pictureBox2.Visible = false;
                        materialLabel1.Visible = true;
                        materialLabel1.Text = "RFID Detected: " + rfidData;
                        materialLabel5.Visible = false;
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading RFID data: {ex.Message}");
            }
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

        private Rectangle DetectLicensePlateRegion(Image<Bgr, byte> img)
        {
            // Convert to grayscale
            var gray = img.Convert<Gray, byte>();

            // Apply Gaussian blur to reduce noise
            var blurred = gray.SmoothGaussian(5);

            // Apply edge detection
            var edges = blurred.Canny(100, 200);

            // Find contours
            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Mat hierarchy = new Mat();
            CvInvoke.FindContours(edges, contours, hierarchy, RetrType.External, ChainApproxMethod.ChainApproxSimple);

            Rectangle licensePlateRegion = Rectangle.Empty;
            double minArea = 5000; // Minimum area threshold for license plate detection

            for (int i = 0; i < contours.Size; i++)
            {
                var contour = contours[i];
                var rect = CvInvoke.BoundingRectangle(contour);

                // Filter by aspect ratio and size
                double aspectRatio = (double)rect.Width / rect.Height;
                if (aspectRatio > 2 && aspectRatio < 6 && rect.Width * rect.Height > minArea)
                {
                    licensePlateRegion = rect;
                    break; // Assuming only one license plate per image
                }
            }

            return licensePlateRegion;
        }
        private void ProcessFrameForOCR(Bitmap frame)
        {
            try
            {
                using (var img = frame.ToImage<Bgr, byte>())
                {
                    // Detect the license plate region
                    Rectangle licensePlateRegion = DetectLicensePlateRegion(img);

                    if (licensePlateRegion != Rectangle.Empty)
                    {
                        // Crop the license plate region
                        var plateImg = img.GetSubRect(licensePlateRegion);

                        // Convert to grayscale and apply thresholding
                        var gray = plateImg.Convert<Gray, byte>();
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
                    else
                    {
                        MessageBox.Show("License plate region not detected.");
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
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (isRfidOn)
            {
                string data = serialPort.ReadLine();  // Reads RFID tag data

                // Check if data corresponds to a valid tag
                if (!string.IsNullOrEmpty(data))
                {
                    this.Invoke(new MethodInvoker(delegate
                    {
                        P1_pictureBox1.Visible = true;  // Show checkmark
                        P1_pictureBox2.Visible = false; // Hide X
                    }));
                }
                else
                {
                    this.Invoke(new MethodInvoker(delegate
                    {
                        P1_pictureBox1.Visible = false; // Hide checkmark
                        P1_pictureBox2.Visible = true;  // Show X
                    }));
                }
            }
        }

        private void materialSwitch1_CheckedChanged(object sender, EventArgs e)
        {
            if (materialSwitch1.Checked)
            {
                // Turn on RFID reading
                isRfidOn = true;
                try
                {
                    serialPort.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error opening RFID port: " + ex.Message);
                }
            }
            else
            {
                // Turn off RFID reading
                isRfidOn = false;
                try
                {
                    serialPort.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error closing RFID port: " + ex.Message);
                }

                // Hide both picture boxes when RFID is off
                P1_pictureBox1.Visible = false;
                P1_pictureBox2.Visible = false;
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
            //new { Pattern = @"([A-Za-z\s]+)", Field = materialTextBox2 }, // First Name
            
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
                        field.Field.Text = match.Groups[1].Value.Trim();
                    }
                }

                // Handle cases where multiple groups are needed (e.g., Name fields)
                var nameMatch = System.Text.RegularExpressions.Regex.Match(cleanedText, @"([A-Z\s]+),\s([A-Z\s]+)\s([A-Z\s]+)");
                if (nameMatch.Success)
                {
                    materialTextBox1.Text = nameMatch.Groups[1].Value.Trim(); // Last Name
                    materialTextBox2.Text = nameMatch.Groups[2].Value.Trim(); // First Name
                    materialTextBox3.Text = nameMatch.Groups[3].Value.Trim(); // Middle Name
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing cleaned text: {ex.Message}");
            }


        }

        // Helper method to extract value based on label and optional delimiter
        private string GetValueFromText(string text, string label, string delimiter = " ")
        {
            // Example logic: extract value following the label
            int labelIndex = text.IndexOf(label);
            if (labelIndex == -1)
                return string.Empty;

            string value = text.Substring(labelIndex + label.Length).Trim();
            int delimiterIndex = value.IndexOf(delimiter);
            if (delimiterIndex != -1)
            {
                value = value.Substring(0, delimiterIndex).Trim();
            }

            return value;
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

        private void timerRfid_Tick(object sender, EventArgs e)
        {
            // Stop the timer to prevent multiple executions
            timerRfid.Stop();

            // Update UI to show RFID not verified after 5 seconds
            P1_pictureBox1.Visible = false;
            P1_pictureBox2.Visible = true;
            materialLabel1.Visible = true;
            materialLabel1.Text = "RFID Tag is not Verified!";
            materialLabel5.Visible = true;
        }

     

        private void btnRecord_Click(object sender, EventArgs e)
        {
            string connStr = "server=localhost;user=root;database=thesis;password=parasathesis;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // SQL query to insert data into the license table
                    string query = @"INSERT INTO license (
                                lastname, firstname, middlename, nationality, sex, 
                                `date of birth`, weight, height, address, 
                                `license number`, `expiration date`, `agency code`, 
                                `blood type`, `eye color`, restrictions, conditions
                            ) VALUES (
                                @lastname, @firstname, @middlename, @nationality, @sex, 
                                @date_of_birth, @weight, @height, @address, 
                                @license_number, @expiration_date, @agency_code, 
                                @blood_type, @eye_color, @restrictions, @conditions
                            )";

                    // Prepare the command
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        // Set parameters from the MaterialTextBoxes
                        cmd.Parameters.AddWithValue("@lastname", materialTextBox1.Text);
                        cmd.Parameters.AddWithValue("@firstname", materialTextBox2.Text);
                        cmd.Parameters.AddWithValue("@middlename", materialTextBox3.Text);
                        cmd.Parameters.AddWithValue("@nationality", materialTextBox4.Text);
                        cmd.Parameters.AddWithValue("@sex", materialTextBox5.Text);
                        cmd.Parameters.AddWithValue("@date_of_birth", DateTime.Parse(materialTextBox6.Text));
                        cmd.Parameters.AddWithValue("@weight", float.Parse(materialTextBox7.Text));
                        cmd.Parameters.AddWithValue("@height", float.Parse(materialTextBox8.Text));
                        cmd.Parameters.AddWithValue("@address", materialTextBox9.Text);
                        cmd.Parameters.AddWithValue("@license_number", materialTextBox10.Text);
                        cmd.Parameters.AddWithValue("@expiration_date", DateTime.Parse(materialTextBox11.Text));
                        cmd.Parameters.AddWithValue("@agency_code", materialTextBox12.Text);
                        cmd.Parameters.AddWithValue("@blood_type", materialTextBox13.Text);
                        cmd.Parameters.AddWithValue("@eye_color", materialTextBox14.Text);
                        cmd.Parameters.AddWithValue("@restrictions", materialTextBox15.Text);
                        cmd.Parameters.AddWithValue("@conditions", materialTextBox16.Text);

                        // Execute the insert command
                        cmd.ExecuteNonQuery();

                        MessageBox.Show("Data successfully added to the database.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }

        
    }
}
   


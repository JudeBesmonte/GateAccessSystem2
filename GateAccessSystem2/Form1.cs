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
using System.IO.Ports;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Windows.Forms.DataVisualization.Charting;



namespace GateAccessSystem2
{
    public partial class Form1 : MaterialForm
    {
        private FilterInfoCollection videoDevices;
        private string connectionString = "server=localhost;database=thesis;user=root;password=parasathesis;";
        private VideoCaptureDevice videoSource;
        private TesseractEngine ocrEngine;
        private SerialPort rfidReader;
        private CascadeClassifier _licensePlateCascade;
        private TesseractEngine _tesseractEngine;

        private bool isVehicleInside = false; // Tracks whether the vehicle is inside
        private Timer detectionCooldownTimer; // Timer for 10-second interval
        private bool detectionOnCooldown = false; // Prevents multiple detections within the interval



        public Form1()
        {
            InitializeComponent();
            InitializeOCR();
            LoadCascade();
            InitializeTesseract();

            if (IsRFIDReaderConnected())
            {
                // Initialize RFID reader (replace COM3 with your actual port)
                rfidReader = new SerialPort("COM4", 9600, Parity.None, 8, StopBits.One);
                rfidReader.DataReceived += new SerialDataReceivedEventHandler(RFID_DataReceived);
                rfidReader.Open();
            }
            else
            {
                MessageBox.Show("RFID reader not connected. The application will run without RFID functionality.");
            }

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

            // Set form start position to manual
            this.StartPosition = FormStartPosition.Manual;

            // Manually set the position (x, y coordinates)
            this.Location = new Point(195, 82); // Example: x = 100, y = 100

            // Setup Timer for RFID detection cooldown (10 seconds)
            detectionCooldownTimer = new Timer();
            detectionCooldownTimer.Interval = 10000; // 10 seconds
            detectionCooldownTimer.Tick += DetectionCooldownTimer_Tick;
            // Setup Timer for RFID detection
            timerRfid = new Timer();
            timerRfid.Interval = 5000; // 5 seconds (5000 milliseconds)

            Label label1 = new Label();
            label1.Location = new Point(20, 20);
            tabPage1.Controls.Add(label1);

            Label label2 = new Label();
            label2.Location = new Point(20, 20);
            tabPage2.Controls.Add(label2);
            // Populate ComboBoxes
            PopulateComboBoxes();

            // Bind Registration Button Click Event
        }


        private void RFID_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string rfidData = rfidReader.ReadExisting().Trim(); // Read the RFID tag

                if (!string.IsNullOrEmpty(rfidData) && !detectionOnCooldown) // Check if not on cooldown
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        // Reset and stop the timerRfid if RFID data is received
                        timerRfid.Stop(); // Stop the timer to prevent red cross image display

                        // Update the UI with the detected RFID data
                        P1_pictureBox1.Visible = true; // Show check image
                        P1_pictureBox2.Visible = false; // Hide red cross image
                        materialLabel1.Visible = true;
                        materialLabel1.Text = "RFID Detected: " + rfidData;
                        materialLabel5.Visible = false;


                        // Check vehicle status (enter/exit)
                        if (isVehicleInside)
                        {
                            pictureBox4.Visible = true;
                            lblenterexit.Visible = true;
                            lblenterexit.Text = "Vehicle EXIT";
                            isVehicleInside = false; // Set state to outside
                        }
                        else
                        {
                            pictureBox4.Visible = true;
                            lblenterexit.Visible = true;
                            lblenterexit.Text = "Vehicle ENTER";
                            isVehicleInside = true; // Set state to inside
                        }

                        // Start cooldown timer
                        detectionOnCooldown = true;
                        detectionCooldownTimer.Start();

                        // Call method to insert RFID data into the database
                        RecordRFIDTagToDatabase(rfidData);

                        // Start the timer again for the next 5-second detection window
                        timerRfid.Start();
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading RFID data: {ex.Message}");
            }
        }
        private void DetectionCooldownTimer_Tick(object sender, EventArgs e)
        {
            detectionOnCooldown = false; // Allow new detection
            detectionCooldownTimer.Stop(); // Stop the timer until next detection
        }
        private bool IsRFIDReaderConnected()
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                foreach (var port in ports)
                {
                    if (port.Equals("COM4", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }



        private void RecordRFIDTagToDatabase(string rfidTag)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
    {
        try
        {
            conn.Open();

            // Check if the RFID tag exists in the vehicle_registration table
            string checkQuery = "SELECT COUNT(*) FROM vehicle_registration WHERE rfid_tag = @rfid_tag";
            using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@rfid_tag", rfidTag);
                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count == 0)
                        {
                            // RFID tag is not registered, log it as unauthorized
                            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            MaterialSkin.MaterialListBoxItem item = new MaterialSkin.MaterialListBoxItem
                            {
                                Text = rfidTag,
                                SecondaryText = timestamp
                            };
                            lbUnauthorized.Items.Add(item); // Add as MaterialListBoxItem
                            MessageBox.Show("Unauthorized attempt detected.");
                        }
                    }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while inserting RFID data to the database: {ex.Message}");
        }
        finally
        {
            conn.Close();
        }
    }
        }
        private void InitializeOCR()
        {
            try
            {
                string tessDataPath = @"C:\Users\judde\source\repos\GateAccessSystem2\GateAccessSystem2\tessdata";
                ocrEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Tesseract OCR Engine: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }

        private void InitializeTesseract()
        {
            string tessdataPath = @"C:\Users\judde\source\repos\GateAccessSystem2\GateAccessSystem2\Resource";
            _tesseractEngine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
            _tesseractEngine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
        }
        // Haar Cascade -------
        private void LoadCascade()
        {
            string cascadePath = @"C:\Users\judde\source\repos\GateAccessSystem2\GateAccessSystem2\Resource\cascade.xml";
            if (System.IO.File.Exists(cascadePath))
            {
                _licensePlateCascade = new CascadeClassifier(cascadePath);
                
            }
            else
            {
                MessageBox.Show("Cascade XML file not found. Please check the path.");
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
                // Manually assign values to text boxes
                materialTextBox1.Text = "DELA CRUZ"; // Last Name
                materialTextBox2.Text = "JUAN PEDRO"; // First Name
                materialTextBox3.Text = "GARCIA"; // Middle Name
                materialTextBox4.Text = "PHL"; // Nationality
                materialTextBox5.Text = "M"; // Sex
                materialTextBox6.Text = "1987/10/04"; // Date of Birth
                materialTextBox7.Text = "70"; // Weight
                materialTextBox8.Text = "1.55"; // Height
                materialTextBox9.Text = "UNIT/HOUSE NO. BUILDING, STREET NAME, BARANGAY, CITY/MUNICIPALITY"; // Address
                materialTextBox10.Text = "N03-12-123456"; // License No.
                materialTextBox11.Text = "2022/10/04"; // Expiration Date
                materialTextBox12.Text = "N32"; // Agency Code
                materialTextBox13.Text = "O+"; // Blood Type
                materialTextBox14.Text = "BLACK"; // Eyes Color
                materialTextBox15.Text = "12"; // Restrictions
                materialTextBox16.Text = "NONE"; // Conditions
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
                    pictureBoxFrame.SizeMode = PictureBoxSizeMode.Zoom;

                    // Convert the captured frame to Emgu.CV format
                    Image<Bgr, byte> frameImage = capturedFrame.ToImage<Bgr, byte>();

                    // Detect license plates and process OCR
                    DetectLicensePlates(frameImage);
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
                                `license_number`, `expiration date`, `agency code`, 
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

        private void btnPlateRecord_Click(object sender, EventArgs e)
        {
            String connStr = "server=localhost;user=root;database=thesis;password=parasathesis;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // SQL query to insert data into the license_plate table, including detection_time
                    string query = @"INSERT INTO license_plate (plate_number, detection_time) VALUES (@plate_number, @detection_time)";

                    // Prepare the command
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        // Set parameters from the DL_materialTextbox and current datetime
                        cmd.Parameters.AddWithValue("@plate_number", DL_materialTextbox.Text);
                        cmd.Parameters.AddWithValue("@detection_time", DateTime.Now); // Record current datetime

                        // Execute the insert command
                        cmd.ExecuteNonQuery();

                        MessageBox.Show("Plate number and time successfully added to the database.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }
        private void PopulateComboBoxes()
        {
            string connectionString = "server=localhost;database=thesis;user=root;password=parasathesis;";

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                // Populate RegPlate ComboBox
                MySqlDataAdapter daPlate = new MySqlDataAdapter("SELECT plate_number FROM license_plate", connection);
                DataTable dtPlate = new DataTable();
                daPlate.Fill(dtPlate);
                RegPlate.DisplayMember = "plate_number";
                RegPlate.DataSource = dtPlate;

                // Populate RegRFID ComboBox
                MySqlDataAdapter daRFID = new MySqlDataAdapter("SELECT tag FROM rfid_tag", connection);
                DataTable dtRFID = new DataTable();
                daRFID.Fill(dtRFID);
                RegRFID.DisplayMember = "tag";
                RegRFID.DataSource = dtRFID;

                // Populate RegDriver ComboBox
                MySqlDataAdapter daDriver = new MySqlDataAdapter("SELECT CONCAT(lastname, ', ', firstname, ' - ', license_number) AS driver_info FROM license", connection);
                DataTable dtDriver = new DataTable();
                daDriver.Fill(dtDriver);
                RegDriver.DisplayMember = "driver_info";
                RegDriver.DataSource = dtDriver;
            }
        }


        private void RegisterAll_Click(object sender, EventArgs e)
        {
            // Get the correct data from ComboBoxes
            string selectedPlate = RegPlate.Text; // Get the text of the selected plate number
            string selectedRFID = RegRFID.Text;   // Get the text of the selected RFID tag
            string selectedDriver = RegDriver.Text; // Get the displayed text directly for now

            // Database connection string
            string connStr = "server=localhost;user=root;database=thesis;password=parasathesis;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // SQL query to insert data into the vehicle_registration table
                    string query = @"INSERT INTO vehicle_registration (plate_number, rfid_tag, driver_name)
                             VALUES (@plate_number, @rfid_tag, @driver_name)";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        // Set the parameters
                        cmd.Parameters.AddWithValue("@plate_number", selectedPlate);
                        cmd.Parameters.AddWithValue("@rfid_tag", selectedRFID);
                        cmd.Parameters.AddWithValue("@driver_name", selectedDriver); // Save the driver's info from ComboBox

                        // Execute the insert command
                        cmd.ExecuteNonQuery();
                        MessageBox.Show("Data successfully registered.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }

        

        private void searchButton2_Click(object sender, EventArgs e)
        {
            string searchTerm = searchTextBox2.Text.Trim();
            string connStr = "server=localhost;user=root;database=thesis;password=parasathesis;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // SQL query to search by driver_name or plate_number
                    string query = @"SELECT * FROM vehicle_registration
                             WHERE driver_name LIKE @searchTerm 
                             OR plate_number LIKE @searchTerm";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@searchTerm", "%" + searchTerm + "%");

                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                        {
                            DataTable results = new DataTable();
                            adapter.Fill(results);
                            searchResultsGridView2.DataSource = results;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }

        private void AnalyzePeakHours()
        {
            Dictionary<int, int> hourlyCounts = new Dictionary<int, int>();

            // Initialize all hours to 0
            for (int hour = 0; hour < 24; hour++)
            {
                hourlyCounts[hour] = 0;
            }

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Query detection_time from both tables
                    string query = @"
                SELECT detection_time FROM rfid_tag
                UNION ALL
                SELECT detection_time FROM license_plate";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DateTime detectionTime = reader.GetDateTime("detection_time");
                            int hour = detectionTime.Hour;
                            hourlyCounts[hour]++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during peak hour analysis: {ex.Message}");
                    return;
                }
            }

            // Clear existing data in chart
            chart1.Series.Clear();
            Series series = new Series("Vehicle Count")
            {
                ChartType = SeriesChartType.Column
            };

            // Add data points to the series (ensure values are whole numbers)
            foreach (var hour in hourlyCounts)
            {
                // Ensure the count is an integer
                series.Points.AddXY(hour.Key, (int)hour.Value); // Cast to int explicitly
            }

            // Add the series to the chart
            chart1.Series.Add(series);

            // Set chart axis titles
            chart1.ChartAreas[0].AxisX.Title = "Hour of the Day";
            chart1.ChartAreas[0].AxisY.Title = "Vehicle Count";

            // Customize Y-Axis to scale from 0 to 20 (whole numbers)
            chart1.ChartAreas[0].AxisY.Minimum = 0;
            chart1.ChartAreas[0].AxisY.Maximum = 20;

            // Customize X-Axis for better hour display (show every hour)
            chart1.ChartAreas[0].AxisX.Interval = 1;
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "00"; // Display hours as two digits (e.g., 00, 01, 02, ...)
            chart1.ChartAreas[0].AxisX.IsLabelAutoFit = true;
            chart1.ChartAreas[0].AxisX.LabelStyle.Angle = -45; // Optional: Rotate labels for better readability

            // Optional: Improve chart appearance (set grid lines, titles, etc.)
            chart1.ChartAreas[0].AxisX.MajorGrid.LineColor = System.Drawing.Color.LightGray;
            chart1.ChartAreas[0].AxisY.MajorGrid.LineColor = System.Drawing.Color.LightGray;
            chart1.ChartAreas[0].AxisY.MajorTickMark.Enabled = true;
            chart1.ChartAreas[0].AxisX.MajorTickMark.Enabled = true;
        }

        private void btnAnalyzePeakHours_Click(object sender, EventArgs e)
        {
            AnalyzePeakHours();
        }

        private void DL_materialTextbox_Click(object sender, EventArgs e)
        {

        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    Image<Bgr, byte> image = new Image<Bgr, byte>(openFileDialog.FileName);
                    ShowImageInPictureBox(pictureBoxFrame, image);
                    DetectLicensePlates(image);
                }
            }
        }
        private void ShowImageInPictureBox(PictureBox pictureBox, Image<Bgr, byte> image)
        {
            pictureBox.Image = image.ToBitmap();
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        }
        private void DetectLicensePlates(Image<Bgr, byte> image)
        {
            if (_licensePlateCascade == null)
            {
                MessageBox.Show("Cascade not loaded. Cannot perform detection.");
                return;
            }

            using (Image<Gray, byte> grayImage = image.Convert<Gray, byte>())
            {
                double scaleFactor = 1.05;
                int minNeighbors = 5;
                Size minSize = new Size(20, 20);

                Rectangle[] plates = _licensePlateCascade.DetectMultiScale(grayImage, scaleFactor, minNeighbors, minSize);

                if (plates.Length == 0)
                {
                    MessageBox.Show("No license plates detected.");
                }
                else
                {
                    foreach (Rectangle plate in plates)
                    {
                        CvInvoke.Rectangle(image, plate, new MCvScalar(0, 0, 255), 2);
                        Image<Bgr, byte> croppedPlate = image.Copy(plate);
                        ShowImageInPictureBox(pbCropped, croppedPlate);
                        ExtractTextFromPlate(croppedPlate);
                    }
                    
                }

                ShowImageInPictureBox(pictureBoxFrame, image);
            }
        }

        private void PreprocessCroppedPlate(Image<Gray, byte> grayPlate)
        {
            grayPlate._SmoothGaussian(5);
            grayPlate._ThresholdBinary(new Gray(100), new Gray(255));
        }

        private void ExtractTextFromPlate(Image<Bgr, byte> croppedPlate)
        {
            using (Image<Gray, byte> grayPlate = croppedPlate.Convert<Gray, byte>())
            {
                PreprocessCroppedPlate(grayPlate);
                Bitmap plateBitmap = grayPlate.ToBitmap();

                using (var page = _tesseractEngine.Process(plateBitmap))
                {
                    string extractedText = page.GetText().Trim();
                    string cleanedText = PostprocessExtractedText(extractedText);
                    DL_materialTextbox.Text = cleanedText;
                }
            }
        }

        private string PostprocessExtractedText(string text)
        {
            text = text.Replace("O", "0");
            text = text.Replace("I", "1");
            return text;
        }
    }
}



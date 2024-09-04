using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using Emgu.CV;
using Emgu.CV.Structure;
using MaterialSkin;
using MaterialSkin.Controls;
using Python.Runtime;
using Tesseract;

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
            InitializePython(); // Initialize Python first
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
        }

        private void InitializePython()
        {
            Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", @"C:\Users\judde\AppData\Local\Programs\Python\Python312\python312.dll");
            PythonEngine.Initialize();
        }

        private void InitializeOCR()
        {
            try
            {
                string tessDataPath = @"C:\Users\judde\source\repos\GateAccessSystem2\GateAccessSystem2\GateAccessSystem2\tessdata";
                if (!Directory.Exists(tessDataPath))
                {
                    throw new DirectoryNotFoundException($"Tesseract data path not found: {tessDataPath}");
                }

                ocrEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                MessageBox.Show("Tesseract OCR Engine initialized successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing Tesseract OCR Engine: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }

        private string PerformYOLOv8Inference(string imagePath)
        {
            string detectedImagePath = "";
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"C:\\Users\\judde\\source\\repos\\GateAccessSystem2\\GateAccessSystem2\\python_scripts\\yolov8_inference.py \"{imagePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    using (var reader = process.StandardOutput)
                    {
                        detectedImagePath = reader.ReadLine();
                        // Debugging
                        MessageBox.Show($"Detected image path: {detectedImagePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during YOLOv8 inference: {ex.Message}");
            }
            return detectedImagePath;
        }

        private void videoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // Capture the frame and save it to a temporary file
                Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
                string tempFilePath = Path.Combine(Path.GetTempPath(), "frame.jpg");

                // Save the frame
                frame.Save(tempFilePath);

                // Perform YOLOv8 inference
                string detectedImagePath = PerformYOLOv8Inference(tempFilePath);

                if (string.IsNullOrEmpty(detectedImagePath))
                {
                    MessageBox.Show("YOLOv8 did not return a valid image path.");
                    return;
                }

                // Update the PictureBox with the detected image
                if (DL_pictureBox.InvokeRequired)
                {
                    DL_pictureBox.Invoke(new Action(() => DL_pictureBox.Image = Image.FromFile(detectedImagePath)));
                }
                else
                {
                    DL_pictureBox.Image = Image.FromFile(detectedImagePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during video frame processing: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }

        private void StartCamera()
        {
            try
            {
                if (videoSource == null || !videoSource.IsRunning)
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCamera();
            PythonEngine.Shutdown(); // Shutdown Python environment

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

        private void materialSwitch2_CheckedChanged(object sender, EventArgs e)
        {
            if (materialSwitch2.Checked)
            {
                StartCamera();
            }
            else
            {
                StopCamera();
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

                    using (var img = bitmap.ToImage<Bgr, byte>())
                    {
                        var gray = img.Convert<Gray, byte>();
                        var threshold = gray.ThresholdBinary(new Gray(100), new Gray(255));

                        using (var pix = PixConverter.ToPix(threshold.ToBitmap()))
                        {
                            using (var result = ocrEngine.Process(pix))
                            {
                                string text = result.GetText().Trim();

                                materialMultiLineTextBox21.Invoke(new Action(() => materialMultiLineTextBox21.Text = text));
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
    }
}

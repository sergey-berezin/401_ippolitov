using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using RecognitionLibrary;


namespace RecognitionAppWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationToken token;
        public ImmutableList<BitmapImage> images;
        public string[] filenames;
        public string directoryPath;

        public MainWindow()
        {
            InitializeComponent();
            token = cts.Token;
            directoryPath = "";
            listBox_Images.ItemsSource = new List<BitmapImage>();
            images = ImmutableList.Create<BitmapImage>();
        }


        private void Open_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DirectoryPath.Text = "Open clicked";
                var dlg = new System.Windows.Forms.FolderBrowserDialog();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    DirectoryPath.Text = "Current directory:" + dlg.SelectedPath;
                    directoryPath = dlg.SelectedPath;

                    filenames = Directory.GetFiles(directoryPath);
                    listBox_Images.ItemsSource = new List<BitmapImage>();
                    images = ImmutableList.Create<BitmapImage>();
                    foreach (var filename in filenames) {
                        try
                        {
                            images = images.Add(new BitmapImage(new Uri(filename)));
                        } catch
                        {
                            continue;
                        }
                    }
                    listBox_Images.ItemsSource = images;
                }
            }
            catch (Exception exception)
            {
                DirectoryPath.Text = "An error was occurred";
                DirectoryPath.Text = exception.Message;
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }
        
        private BitmapImage BitmapImageFromBitmap(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
        private Bitmap BitmapFromBitmapImage(BitmapImage bitmapImage)
        {
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            Open.IsEnabled = false;
            Start.IsEnabled = false;
            var detectionResults = new ConcurrentQueue<Tuple<string, YoloV4Result>>();
            cts = new CancellationTokenSource();
            token = cts.Token;
            try
            {

                var recognitionTask = Task.Factory.StartNew(_ =>
                {
                    ObjectDetection.Detect(directoryPath, cts, detectionResults);
                }, cts, cts.Token);

                var writeResultsTask = Task.Factory.StartNew(() =>
                {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Status.Text = "Detection started";
                }));
                while (recognitionTask.Status == TaskStatus.Running)
                {
                    while (detectionResults.TryDequeue(out Tuple<string, YoloV4Result> result))
                    {
                        var filename = result.Item1;
                        int index = Array.FindIndex(filenames, val => val == filename);
                        if (index < 0)
                        {
                            continue;
                        }
                        var bitmap = BitmapFromBitmapImage(images[index]);
                        var g = Graphics.FromImage(bitmap);

                        var detectedObject = result.Item2;
                        var x1 = detectedObject.BBox[0];
                        var y1 = detectedObject.BBox[1];
                        var x2 = detectedObject.BBox[2];
                        var y2 = detectedObject.BBox[3];

                        g.DrawRectangle(Pens.Red, x1, y1, x2 - x1, y2 - y1);
                        var brushes = new SolidBrush(Color.FromArgb(30, Color.Red));
                            
                        g.FillRectangle(brushes, x1, y1, x2 - x1, y2 - y1);
                        g.DrawString(detectedObject.Label + ',' + detectedObject.Confidence.ToString(),
                            new Font("Arial", 16), Brushes.Indigo, new PointF(x1, y1));


                        images = images.RemoveAt(index);
                        images = images.Insert(index, BitmapImageFromBitmap(bitmap));
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            listBox_Images.ItemsSource = images;
                            Status.Text = "Detected: " + detectedObject.Label;
                        }));
                        }
                    }
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Status.Text = "Detected ended";
                    }));
                }, TaskCreationOptions.LongRunning);
                await Task.WhenAll(recognitionTask, writeResultsTask);
            }
            catch (Exception ex)
            {
                Status.Text = ex.Message; 
            }
            finally
            {
                Open.IsEnabled = true;
                Start.IsEnabled = true;
            }
        }
    }
}

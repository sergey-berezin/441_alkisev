using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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
using Newtonsoft.Json;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Advanced;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly string addr = "http://localhost:5000";
        
        public List<Contracts.Image> Images = new();
        public CancellationTokenSource cts = new();
        public StorageHttpClient client = new(addr);
        public bool ComparisonRunning { get; private set; }

        public ICommand LoadImages { get; private set; }
        public ICommand Compare { get; private set; }
        public ICommand Cancel { get; private set; }
        public ICommand Clear { get; private set; }
        public ICommand ClearStorage { get; private set; }
        public ICommand UpdateStorageInfo { get; private set; }
        public ObservableCollection<Contracts.Image> SavedImages { get; private set; }

        public MainWindow()
        {
            ComparisonRunning = false;

            LoadImages = new RelayCommand(async _ => { await DoLoadImages(); }, _ => { return CanLoadImages(); });
            Compare = new RelayCommand(async _ => { await DoCompare(); }, _ => { return CanCompare(); });
            Cancel = new RelayCommand(_ => { DoCancel(); }, _ => { return CanCancel(); });
            Clear = new RelayCommand(_ => { DoClear(); }, _ => { return CanClear(); });
            ClearStorage = new RelayCommand(async _ => { await DoClearStorage(); }, _ => { return CanClearStorage(); });
            UpdateStorageInfo = new RelayCommand(async _ => { await DoUpdateStorageInfo(); }, _ => { return CanUpdateStorageInfo(); });

            SavedImages = new ObservableCollection<Contracts.Image>();

            InitializeComponent();
            MainGrid.DataContext = this;
        }

        private static TextBlock CreateTextBlock(string distance, string similarity)
        {
            var tb = new TextBlock();
            tb.Inlines.Add(new Run(distance) { Foreground = Brushes.Red });
            tb.Inlines.Add(new Run("\n  " + similarity) { Foreground = Brushes.LightGreen });
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            return tb;
        }
        private bool CanLoadImages()
        {
            return !ComparisonRunning;
        }
        private async Task DoLoadImages()
        {
            try
            {
                DoClear();
                var ofd = new OpenFileDialog { Multiselect = true };
                var response = ofd.ShowDialog();
                if (response == true)
                {
                    foreach (var path in ofd.FileNames)
                    {
                        byte[] data = await System.IO.File.ReadAllBytesAsync(path, cts.Token);
                        var memStream = new MemoryStream(data);
                        var srcImageData = await SixLabors.ImageSharp.Image.LoadAsync<Rgb24>(memStream, cts.Token);
                        srcImageData.Mutate(ctx =>
                        {
                            ctx.Resize(new ResizeOptions
                            {
                                Size = new SixLabors.ImageSharp.Size(112, 112),
                                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Crop
                            });
                        });
                        memStream = new MemoryStream();
                        srcImageData.Save(memStream, srcImageData.GetConfiguration().ImageFormatsManager.FindEncoder(PngFormat.Instance));
                        data = memStream.ToArray();

                        var details = new Contracts.ImageDetails
                        {
                            Data = data
                        };
                        var image = new Contracts.Image
                        {
                            Name = path,
                            Details = details,
                            Hash = Contracts.Image.GetHash(details.Data),
                        };
                        var id = await client.PostImageToService(image);
                        image.Id = id;
                        Images.Add(image);
                    }
                }
                ComparisonData.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                ComparisonData.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                var tb = CreateTextBlock("Distance", "Similarity");
                Grid.SetColumn(tb, 0);
                Grid.SetRow(tb, 0);
                ComparisonData.Children.Add(tb);
                for (int i = 0; i < Images.Count; i++)
                {
                    ComparisonData.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                    ComparisonData.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    var image = new System.Windows.Controls.Image
                    {
                        Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i].Details.Data)
                    };
                    Grid.SetColumn(image, 0);
                    Grid.SetRow(image, i + 1);
                    ComparisonData.Children.Add(image);

                    image = new System.Windows.Controls.Image
                    {
                        Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i].Details.Data)
                    };
                    Grid.SetColumn(image, i + 1);
                    Grid.SetRow(image, 0);
                    ComparisonData.Children.Add(image);
                }
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
        private bool CanCompare()
        {
            return !ComparisonRunning && Images.Count > 0;
        }

        private float[]? GetEmbedding(int i)
        {
            if (Images[i] is null)
            {
                return null;
            }
            float[] embed;
            embed = new float[Images[i].Embedding.Length / 4];
            Buffer.BlockCopy(Images[i].Embedding, 0, embed, 0, Images[i].Embedding.Length);
            return embed;
        }
        private static float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x * x).Sum());
        public static float Distance(float[] v1, float[] v2) => Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());
        public static float Similarity(float[] v1, float[] v2) => v1.Zip(v2).Select(p => p.First * p.Second).Sum();
        private async Task DoCompare()
        {
            try
            {
                ComparisonRunning = true;
                if (cts.IsCancellationRequested)
                {
                    cts = new CancellationTokenSource();
                }
                var n = Images.Count;
                if (ComparisonData.Children.Count > 2 * n + 1)
                {
                    ComparisonData.Children.RemoveRange(2 * n + 1, ComparisonData.Children.Count - 2 * n - 1);
                }
                var progress_step = 100.0 / (n * n);
                ComparisonPB.Value = 0;
                ComparisonPB.Foreground = Brushes.Green;
                try
                {
                    for (int i = 0; i < n && !cts.IsCancellationRequested; ++i)
                    {
                        var id = Images[i].Id;
                        var image = await client.GetImageFromService(id);
                        if(image is not null)
                        {
                            Images[i] = image;
                        }
                    }
                    for (int i = 0; i < n && !cts.IsCancellationRequested; ++i)
                    {
                        float[]? embed1 = GetEmbedding(i);
                        for (int j = 0; j < n && !cts.IsCancellationRequested; ++j)
                        {
                            float[]? embed2 = GetEmbedding(j);
                            if (embed1 is not null && embed2 is not null)
                            {
                                var distance = Distance(embed1, embed2);
                                var similarity = Similarity(embed1, embed2);
                                var tb = CreateTextBlock(distance.ToString("F4"), similarity.ToString("F4"));
                                Grid.SetColumn(tb, i + 1);
                                Grid.SetRow(tb, j + 1);
                                ComparisonData.Children.Add(tb);
                            }
                            else
                            {
                                var tb = CreateTextBlock("Unkown", "Unkown");
                                Grid.SetColumn(tb, i + 1);
                                Grid.SetRow(tb, j + 1);
                                ComparisonData.Children.Add(tb);
                            }
                            ComparisonPB.Value += progress_step;
                        }
                    }
                }
                catch (OperationCanceledException)
                { }
                if (!cts.IsCancellationRequested)
                {
                    ComparisonPB.Value = 100;
                }
                else
                {
                    ComparisonPB.Foreground = Brushes.Red;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
            finally
            {
                ComparisonRunning = false;
            }
        }

        private bool CanCancel()
        {
            return ComparisonRunning;
        }

        private void DoCancel()
        {
            cts.Cancel();
        }

        private bool CanClear()
        {
            return !ComparisonRunning && Images.Count > 0;
        }

        private void DoClear()
        {
            ComparisonPB.Value = 0;
            ComparisonData.Children.Clear();
            ComparisonData.RowDefinitions.Clear();
            ComparisonData.ColumnDefinitions.Clear();
            Images.Clear();
        }

        private bool CanClearStorage()
        {
            return true;
        }

        private async Task DoClearStorage()
        {
            try
            {
                DoClear();
                await client.DeleteImagesFromService();
                await DoUpdateStorageInfo();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private bool CanUpdateStorageInfo()
        {
            return true;
        }

        private async Task DoUpdateStorageInfo()
        {
            try
            {
                var images = await client.GetImagesFromService();
                SavedImages.Clear();
                foreach(var img in images)
                {
                    SavedImages.Add(img);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}

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

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // TODO:
        //   - image resize
        //   - requests retry

        private static readonly string addr = "http://localhost:5000";
        
        public List<Contracts.Image> Images = new();
        public CancellationTokenSource cts = new();
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
        private static async Task<Contracts.Image[]> GetImagesFromService()
        {
            var client = new HttpClient();
            var response = await client.GetAsync($"{addr}/images");
            var ret = JsonConvert.DeserializeObject<Contracts.Image[]>(response.Content.ReadAsStringAsync().Result);
            if (ret is null)
            {
                return Array.Empty<Contracts.Image>();
            }
            return ret;
        }

        private static async Task<Contracts.Image?> GetImageFromService(int id)
        {
            var client = new HttpClient();
            var response = await client.GetAsync($"{addr}/images/{id}");
            var ret = JsonConvert.DeserializeObject<Contracts.Image>(response.Content.ReadAsStringAsync().Result);
            return ret;
        }

        private static async Task<int> PostImageToService(Contracts.Image img)
        {
            var client = new HttpClient();
            var data = new StringContent(JsonConvert.SerializeObject(img), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{addr}/images", data);
            if(response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                MessageBox.Show(response.Content.ReadAsStringAsync().Result);
            }
            var responseString = await response.Content.ReadAsStringAsync();
            return Int32.Parse(responseString);
        }

        private static async Task DeleteImagesFromService()
        {
            var client = new HttpClient();
            var response = await client.DeleteAsync($"{addr}/images");
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
            DoClear();
            var ofd = new OpenFileDialog { Multiselect = true };
            var response = ofd.ShowDialog();
            if (response == true)
            {
                foreach (var path in ofd.FileNames)
                {
                    var details = new Contracts.ImageDetails
                    {
                        Data = await System.IO.File.ReadAllBytesAsync(path),
                    };
                    var image = new Contracts.Image
                    {
                        Name = path,
                        Details = details,
                        Hash = Contracts.Image.GetHash(details.Data),
                    };
                    var id = await PostImageToService(image);
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
                        var image = await GetImageFromService(id);
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
                await DeleteImagesFromService();
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
                var images = await GetImagesFromService();
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

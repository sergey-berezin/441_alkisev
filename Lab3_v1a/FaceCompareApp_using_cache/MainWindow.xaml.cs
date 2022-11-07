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
using System.ComponentModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using FacesSimilarity;
using System.Threading;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Collections;

namespace FaceCompareApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<Tuple<byte[], string>> Images;
        public FacesComparator FacesComparator;
        public CancellationTokenSource cts;
        public ObservableCollection<EfClasses.Image> CachedImages { get; private set; }
        private SemaphoreSlim dbLock;
        public bool ComparisonRunning { get; private set; }

        public ICommand LoadImages { get; private set; }
        public ICommand Compare { get; private set; }
        public ICommand Cancel { get; private set; }
        public ICommand Clear { get; private set; }
        public ICommand DeleteRecord { get; private set; }

        public MainWindow()
        {
            Images = new List<Tuple<byte[], string>>();
            FacesComparator = new FacesComparator();
            cts = new CancellationTokenSource();
            ComparisonRunning = false;
            LoadImages = new RelayCommand(_ => { DoLoadImages(); }, _ => { return CanLoadImages(); });
            Compare = new RelayCommand(_ => { DoCompare(); }, _ => { return CanCompare(); });
            Cancel = new RelayCommand(_ => { DoCancel(); }, _ => { return CanCancel(); });
            Clear = new RelayCommand(_ => { DoClear(); }, _ => { return CanClear(); });
            DeleteRecord = new RelayCommand(_ => { DoDeleteRecord(); }, _ => { return CanDeleteRecord(); });

            CachedImages = new ObservableCollection<EfClasses.Image>();
            dbLock = new SemaphoreSlim(1, 1);
            using (var db = new EfClasses.ImagesContext())
            {
                foreach(var image in db.Images)
                {
                    CachedImages.Add(image);
                }
            }

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
        private void DoLoadImages()
        {
            DoClear();
            var ofd = new OpenFileDialog { Multiselect = true };
            var response = ofd.ShowDialog();
            if (response == true)
            {
                foreach (var path in ofd.FileNames)
                {
                    Images.Add(Tuple.Create(System.IO.File.ReadAllBytes(path), path));
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
                    Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i].Item1)
                };
                Grid.SetColumn(image, 0);
                Grid.SetRow(image, i + 1);
                ComparisonData.Children.Add(image);

                image = new System.Windows.Controls.Image
                {
                    Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i].Item1)
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

        private async Task<float[]> GetEmbedding(int i)
        {
            EfClasses.Image imageRecord = null;
            await dbLock.WaitAsync();
            using (var db = new EfClasses.ImagesContext())
            {
                string hash = EfClasses.Image.GetHash(Images[i].Item1);
                var q = db.Images.Where(x => x.Hash == hash)
                    .Include(x => x.Details)
                    .Where(x => Equals(x.Details.Data, Images[i].Item1));
                if(q.Any())
                {
                    imageRecord = q.First();
                }
            }
            dbLock.Release();

            float[] embed;

            if (imageRecord is not null)
            {
                embed = new float[imageRecord.Embedding.Length / 4];
                Buffer.BlockCopy(imageRecord.Embedding, 0, embed, 0, imageRecord.Embedding.Length);
            }
            else
            {
                var stream = new MemoryStream(Images[i].Item1);
                var image = await SixLabors.ImageSharp.Image.LoadAsync<Rgb24>(stream, cts.Token);
                embed = await FacesComparator.GetEmbeddingsAsync(image, cts.Token);

                await dbLock.WaitAsync();
                using (var db = new EfClasses.ImagesContext())
                {
                    var imageDetailsRecord = new EfClasses.ImageDetails { Data = Images[i].Item1 };
                    var byteArray = new byte[embed.Length * 4];
                    Buffer.BlockCopy(embed, 0, byteArray, 0, byteArray.Length);
                    imageRecord = new EfClasses.Image
                    {
                        Name = Images[i].Item2,
                        Embedding = byteArray,
                        Details = imageDetailsRecord,
                        Hash = EfClasses.Image.GetHash(Images[i].Item1)
                    };

                    db.Add(imageRecord);
                    CachedImages.Add(imageRecord);
                    db.SaveChanges();
                }
                dbLock.Release();
            }
            return embed;
        }

        private async void DoCompare()
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
                        var embed1 = await GetEmbedding(i);

                        for (int j = 0; j < n && !cts.IsCancellationRequested; ++j)
                        {
                            var embed2 = await GetEmbedding(j);

                            var distance = FacesComparator.Distance(embed1, embed2);
                            var similarity = FacesComparator.Similarity(embed1, embed2);
                            var tb = CreateTextBlock(distance.ToString("F4"), similarity.ToString("F4"));
                            Grid.SetColumn(tb, i + 1);
                            Grid.SetRow(tb, j + 1);
                            ComparisonData.Children.Add(tb);
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
                ComparisonRunning = false;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
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

        private bool CanDeleteRecord()
        {
            return CachedImagesLB.SelectedItem != null;
        }

        private async void DoDeleteRecord()
        {
            try
            {
                var item = CachedImages[CachedImagesLB.SelectedIndex];
                await dbLock.WaitAsync();
                using (var db = new EfClasses.ImagesContext())
                {
                    var imageRecord = db.Images.Where(x => x.Id == item.Id).Include(x => x.Details).First();
                    if (imageRecord == null)
                    {
                        dbLock.Release();
                        return;
                    }
                    db.Details.Remove(imageRecord.Details);
                    db.Images.Remove(imageRecord);
                    db.SaveChanges();
                    CachedImages.Remove(item);
                }
                dbLock.Release();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}

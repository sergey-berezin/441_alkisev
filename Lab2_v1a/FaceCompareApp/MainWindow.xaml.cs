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

namespace FaceCompareApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<byte[]> Images;
        public FacesComparator FacesComparator;
        public CancellationTokenSource cts;
        public bool ComparisonRunning { get; private set; }

        public ICommand LoadImages { get; private set; }
        public ICommand Compare { get; private set; }
        public ICommand Cancel { get; private set; }
        public ICommand Clear { get; private set; }

        public MainWindow()
        {
            Images = new List<byte[]>();
            FacesComparator = new FacesComparator();
            cts = new CancellationTokenSource();
            ComparisonRunning = false;
            LoadImages = new RelayCommand(_ => { DoLoadImages(); }, _ => { return CanLoadImages(); });
            Compare = new RelayCommand(_ => { DoCompare(); }, _ => { return CanCompare(); });
            Cancel = new RelayCommand(_ => { DoCancel(); }, _ => { return CanCancel(); });
            Clear = new RelayCommand(_ => { DoClear(); }, _ => { return CanClear(); });
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
                    Images.Add(System.IO.File.ReadAllBytes(path));
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
                    Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i])
                };
                Grid.SetColumn(image, 0);
                Grid.SetRow(image, i + 1);
                ComparisonData.Children.Add(image);

                image = new System.Windows.Controls.Image
                {
                    Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(Images[i])
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
        private async void DoCompare()
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
            for (int i = 0; i < n && !cts.IsCancellationRequested; ++i)
            {
                for (int j = 0; j < n && !cts.IsCancellationRequested; ++j)
                {
                    try
                    {
                        var v = await FacesComparator.CompareAsync(Images[i], Images[j], cts.Token);
                        var tb = CreateTextBlock(v.Item1.ToString("F4"), v.Item2.ToString("F4"));
                        Grid.SetColumn(tb, i + 1);
                        Grid.SetRow(tb, j + 1);
                        ComparisonData.Children.Add(tb);
                        ComparisonPB.Value += progress_step;
                    }
                    catch(OperationCanceledException)
                    { }
                }
            }
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
    }
}

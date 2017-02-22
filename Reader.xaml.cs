using PageProvider.Collection;
using PageProvider.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Reader
{
    public sealed partial class Reader : Page
    {
        public Reader()
        {
            this.InitializeComponent();
            pages.CollectionChanged += Pages_CollectionChanged;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
        }

        private async void Pages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
            {
                if (currentPage >= e.NewStartingIndex && currentPage < e.NewStartingIndex + e.NewItems.Count)
                {
                    var src = await loadImage(currentPage);
                    mangaImage.Source = src;
                    scaleImageToFit();
                    mangaImage.Opacity = 1;
                }
            }
        }

        private VirtualizedRandomAccessCollection<MangaPage> pages = new VirtualizedRandomAccessCollection<MangaPage>();

        private int currentPage = 0;
        private async Task<ImageSource> loadImage(int i)
        {
            var trackedItems = new List<ItemIndexRange>();
            var selected = new ItemIndexRange(i, 1);
            trackedItems.Add(selected);

            int lastIndex = Math.Min(pages.Count - 1, i + 5);
            if (i < lastIndex)
            {
                var newer = new ItemIndexRange(i + 1, (uint)(lastIndex - i));
                trackedItems.Add(newer);
            }
            
            int firstIndex = Math.Max(0, i - 1);
            if (firstIndex < i)
            {
                var older = new ItemIndexRange(firstIndex, (uint)(i - firstIndex));
                trackedItems.Add(older);
            }

            pages.RangesChanged(selected, trackedItems);
            var p = pages[i];
            var src = new SoftwareBitmapSource();
            var bmp = p.Source;
            await src.SetBitmapAsync(bmp);
            return src;
        }

        private void sliderValueChanged(object sender, RangeBaseValueChangedEventArgs args)
        {
            cancelHideSlider();
            currentPage = (int)args.NewValue - 1;
            mangaImage.Opacity = 0;
        }

        private void scaleImageToFit()
        {
            var pg = pages[currentPage];
            if (pg == null || pg.Source == null)
            {
                return;
            }
            var src = pg.Source;
            if (src == null)
            {
                return;
            }
            if (src.PixelWidth == 0 || src.PixelHeight == 0)
            {
                return;
            }
            var scaleFX = imageContainer.ActualWidth / src.PixelWidth;
            var scaleFY = imageContainer.ActualHeight / src.PixelHeight;
            var scale = Math.Min(scaleFX, scaleFY);
            scale = (float)Math.Min(scale, 1);
            var pixelDelta = Math.Abs(scale - imageContainer.ZoomFactor) * Math.Max(src.PixelHeight, src.PixelWidth);
            if (pixelDelta < 5)
            {
                return;
            }

            imageContainer.ChangeView(0, 0, (float)scale);
        }

        private async Task<int> displayPage(int page)
        {
            if (page == currentPage && mangaImage.Opacity == 1)
            {
                return page;
            }
            if (page < pages.Count && page >= 0)
            {
                var src = await loadImage(page);
                mangaImage.Source = src;
                scaleImageToFit();
                mangaImage.Opacity = 1;
                currentPage = page;
                manga.LastReadPageIdx = currentPage;
                if (currentPage == (pages.Count - 1))
                {
                    manga.DoneReading = true;
                }
            }
            return page;
        }

        private async void pageUp()
        {
            if (currentPage < pages.Count - 1)
            {
                currentPage++;
                pageSlider.Value = (currentPage + 1);
                await displayPage(currentPage);
                hideSliderAfterDelay(5000);
            }
        }

        private async void pageDown()
        {
            if (currentPage > 0)
            {
                currentPage--;
                pageSlider.Value = (currentPage + 1);
                await displayPage(currentPage);
                hideSliderAfterDelay(5000);
            }
        }

        private void keyPressHandler(object sender, KeyEventArgs args)
        {
            if (pageSlider.Opacity != 0 && pageSlider.FocusState != FocusState.Unfocused)
            {
                return;
            }
            if (args.VirtualKey == Windows.System.VirtualKey.Right)
            {
                pageUp();
            }

            if (args.VirtualKey == Windows.System.VirtualKey.Left)
            {
                pageDown();
            }
        }

        private Manga manga;
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame.CanGoBack)
            {
                // Show UI in title bar if opted-in and in-app backstack is not empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;
            }
            else
            {
                // Remove the UI from the title bar if in-app back stack is empty.
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            }

            manga = e.Parameter as Manga;
            //var pageSource = await PageDataSource.FromMangaAsync(m);
            pages.SetDataSource(manga.Pages);

            var lastReadPage = manga.LastReadPageIdx;
            //pages = m.Pages;
            currentPage = 0;
            pageSlider.Minimum = 1;
            pageSlider.Maximum = pages.Count;
            if (lastReadPage > 0 && lastReadPage < pages.Count)
            {
                currentPage = lastReadPage;
            }
            pageSlider.Value = currentPage + 1;
            await displayPage(currentPage);
            scaleImageToFit();
            showSliderAfterDelay(100);

            base.OnNavigatedTo(e);
            Window.Current.CoreWindow.KeyDown += keyPressHandler;
            Window.Current.Activate();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Window.Current.CoreWindow.KeyDown -= keyPressHandler;
        }

        private async void pageSlider_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            await displayPage(currentPage);
            hideSliderAfterDelay(5000);
        }

        private async void pageSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            await displayPage(currentPage);
            hideSliderAfterDelay(5000);
        }

        CancellationTokenSource cnclSrcHide;
        private void cancelHideSlider()
        {
            if (cnclSrcHide != null)
            {
                cnclSrcHide.Cancel();
            }
        }
        private async void hideSliderAfterDelay(int delay)
        {
            cancelHideSlider();
            cnclSrcHide = new CancellationTokenSource();
            var t = cnclSrcHide.Token;

            try
            {
                await Task.Delay(delay, t);
            }
            catch (TaskCanceledException e)
            {
                return;
            }
            cnclSrcHide = null;
            pageSlider.Opacity = 0;
            pageSlider.IsEnabled = false;
        }

        CancellationTokenSource cnclSrcShow;
        private void cancelShowSlider()
        {
            if (cnclSrcShow != null)
            {
                cnclSrcShow.Cancel();
            }
        }

        private async void showSliderAfterDelay(int delay)
        {
            cancelShowSlider();
            cnclSrcShow = new CancellationTokenSource();
            var t = cnclSrcShow.Token;

            try
            {
                await Task.Delay(delay, t);
            }
            catch (TaskCanceledException e)
            {
                return;
            }
            cnclSrcShow = null;
            pageSlider.Opacity = 0.6;
            pageSlider.IsEnabled = true;
            cancelHideSlider();
            hideSliderAfterDelay(5000);
        }

        private double latestPressLocationX = 0;
        private double latestPressLocationY = 0;
        private void mangaImage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // the code below makes it so that relativePosition will be < 0.2 if
            // click point is to the left of the first 20% of the image, or within
            // the first 20% of the container (similar for the latest 20%)
            var point_image = e.GetCurrentPoint(mangaImage);
            var point_container = e.GetCurrentPoint(imageContainer);
            if (imageContainer.ActualWidth == 0 || mangaImage.ActualWidth == 0 ||
                imageContainer.ActualHeight == 0 || mangaImage.ActualHeight == 0)
            {
                return;
            }

            var position = point_image.Position.X;
            var max = mangaImage.ActualWidth;

            if (max * imageContainer.ZoomFactor > imageContainer.ActualWidth)
            {
                position = point_container.Position.X;
                max = imageContainer.ActualWidth;
            }

            var relativeX = position / max;
            latestPressLocationX = relativeX;

            position = point_image.Position.Y;
            max = mangaImage.ActualHeight;

            if (max * imageContainer.ZoomFactor > imageContainer.ActualHeight)
            {
                position = point_container.Position.Y;
                max = imageContainer.ActualHeight;
            }

            var relativeY = position / max;
            latestPressLocationY = relativeY;
        }

        //private bool zooming = false;
        private void mangaImage_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var deltaX = -Math.Abs(e.Velocities.Linear.X) * e.Delta.Translation.X;
            var deltaY = -Math.Abs(e.Velocities.Linear.Y) * e.Delta.Translation.Y;
            var deltaZoom = imageContainer.ZoomFactor * e.Delta.Scale;
            imageContainer.ChangeView(imageContainer.HorizontalOffset + deltaX, imageContainer.VerticalOffset + deltaY, deltaZoom);
        }

        private void mangaImage_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            var nominalLimit = (mangaImage.ActualWidth * imageContainer.ZoomFactor / 2);
            var displacement = e.Cumulative.Translation.X;

            if (displacement > nominalLimit)
            {
                pageDown();
            }
            else if (displacement < -nominalLimit)
            {
                pageUp();
            }
        }

        private void mangaImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (latestPressLocationX < 0.2)
            {
                pageDown();
            }
            else if (latestPressLocationX > 0.8)
            {
                pageUp();
            }
            else
            {
                cancelHideSlider();
                cancelShowSlider();
                if (pageSlider.Opacity != 0)
                {
                    hideSliderAfterDelay(100);
                }
                else
                {
                    showSliderAfterDelay(100);
                }
            }
        }

        private void mangaImage_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            cancelShowSlider();
            cancelHideSlider();
            if (pageSlider.Opacity != 0)
            {
                hideSliderAfterDelay(5000);
            }

            if (Math.Abs(imageContainer.ZoomFactor - 1) > 0.01)
            {
                imageContainer.ChangeView(0, 0, 1);
            }
            else
            {
                scaleImageToFit();
            }
        }
    }
}

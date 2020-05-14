﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GroupMeClient.Plugins.Views;
using GroupMeClient.ViewModels.Controls;
using GroupMeClientApi;
using GroupMeClientApi.Models;
using GroupMeClientApi.Models.Attachments;
using GroupMeClientPlugin;
using GroupMeClientPlugin.GroupChat;

namespace GroupMeClient.Plugins.ViewModels
{
    /// <summary>
    /// <see cref="ImageGalleryWindowViewModel"/> provides a ViewModel for the <see cref="ImageGalleryWindow"/> Window.
    /// </summary>
    public class ImageGalleryWindowViewModel : ViewModelBase
    {
        private ImageGalleryWindowViewModel(IMessageContainer groupChat, CacheSession cacheSession, IPluginUIIntegration uiIntegration)
        {
            this.GroupChat = groupChat;
            this.CacheSession = cacheSession;
            this.UIIntegration = uiIntegration;

            this.GroupName = this.GroupChat.Name;

            this.ShowImageDetailsCommand = new RelayCommand<AttachmentImageItem>(this.ShowImageDetails);
            this.LoadMoreCommand = new RelayCommand<ScrollViewer>(async (s) => await this.LoadNextPage(s), true);

            this.SmallDialogManager = new PopupViewModel()
            {
                ClosePopup = new RelayCommand(this.CloseSmallPopupHandler),
                EasyClosePopup = null,
            };

            this.BigDialogManager = new PopupViewModel()
            {
                ClosePopup = new RelayCommand(this.CloseBigPopupHandler),
                EasyClosePopup = new RelayCommand(this.CloseBigPopupHandler),
            };

            this.MessagesWithAttachments =
               this.CacheSession.CacheForGroupOrChat
                   .AsEnumerable()
                   .Where(m => m.Attachments.Count > 0)
                   .OrderByDescending(m => m.CreatedAtTime);

            this.Images = new ObservableCollection<AttachmentImageItem>();
            this.LastPageIndex = -1;
            _ = this.LoadNextPage();
        }

        /// <summary>
        /// Gets a collection of the <see cref="AttachmentImageItem"/>s that should be displayed in this Gallery.
        /// </summary>
        public ObservableCollection<AttachmentImageItem> Images { get; }

        /// <summary>
        /// Gets the name of the <see cref="IMessageContainer"/> this Gallery is displayed for.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Gets the command to execute to show a detailed view for a particular attached image.
        /// </summary>
        public ICommand ShowImageDetailsCommand { get; }

        /// <summary>
        /// Gets the command to execute to load more images into the gallery.
        /// </summary>
        public ICommand LoadMoreCommand { get; }

        /// <summary>
        /// Gets a manager for showing large, top-level popup dialogs.
        /// </summary>
        public PopupViewModel BigDialogManager { get; }

        /// <summary>
        /// Gets a manager for showing smaller, non-top-level popup dialogs.
        /// </summary>
        public PopupViewModel SmallDialogManager { get; }

        private IMessageContainer GroupChat { get; }

        private CacheSession CacheSession { get; }

        private IPluginUIIntegration UIIntegration { get; }

        private IEnumerable<Message> MessagesWithAttachments { get; }

        private int ImagesPerPage { get; } = 100;

        private int LastPageIndex { get; set; }

        /// <summary>
        /// <see cref="GetAttachmentContentUrls(IEnumerable{Attachment})"/> returns a listing of the
        /// URLs for all images, linked images, and video attachments in the provided <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <param name="attachments">The list of <see cref="Attachment"/>s to scan through.</param>
        /// <returns>An array of content URLs.</returns>
        public static string[] GetAttachmentContentUrls(IEnumerable<Attachment> attachments)
        {
            var results = new List<string>();
            foreach (var attachment in attachments)
            {
                if (attachment is ImageAttachment imageAttachment)
                {
                    results.Add($"{imageAttachment.Url}");
                }
                else if (attachment is LinkedImageAttachment linkedImageAttachment)
                {
                    results.Add($"{linkedImageAttachment.Url}");
                }
                else if (attachment is VideoAttachment videoAttachment)
                {
                    results.Add(videoAttachment.PreviewUrl);
                }
            }

            return results.ToArray();
        }

        private async Task LoadNextPage(ScrollViewer scrollViewer = null)
        {
            if (this.MessagesWithAttachments == null)
            {
                return;
            }

            double originalOffset = scrollViewer?.VerticalOffset ?? 0.0;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.LastPageIndex += 1;

                var range = this.MessagesWithAttachments.Skip(this.LastPageIndex * this.ImagesPerPage).Take(this.ImagesPerPage);

                foreach (var msg in range)
                {
                    var imageUrls = GetAttachmentContentUrls(msg.Attachments);
                    for (int i = 0; i < imageUrls.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(imageUrls[i]))
                        {
                            var entry = new AttachmentImageItem($"{imageUrls[i]}.preview", msg, i, this.GroupChat.Client.ImageDownloader);
                            this.Images.Add(entry);
                        }
                    }
                }

                if (originalOffset != 0)
                {
                    ScrollChangedEventHandler delayedUpdateHandler = null;
                    int skip = 0;
                    delayedUpdateHandler = (s, e) =>
                    {
                        scrollViewer.ScrollToVerticalOffset(originalOffset);

                        if ((int)e.VerticalOffset == (int)originalOffset && skip > 1)
                        {
                            scrollViewer.ScrollChanged -= delayedUpdateHandler;
                        }

                        skip++;
                    };

                    scrollViewer.ScrollChanged += delayedUpdateHandler;
                }
            });
        }

        private void ShowImageDetails(AttachmentImageItem item)
        {
            if (item == null)
            {
                return;
            }

            var currentItem = this.Images.First(x => x.Message.Id == item.Message.Id);
            var currentIndex = this.Images.IndexOf(currentItem);
            var previousItem = currentIndex > 0 ? this.Images[currentIndex - 1] : null;
            var nextItem = currentIndex < this.Images.Count - 1 ? this.Images[currentIndex + 1] : null;

            var dialog = new ImageDetailsControlViewModel(
                message: item.Message,
                imageIndex: item.ImageIndex,
                downloader: this.GroupChat.Client.ImageDownloader,
                showPopupAction: this.ShowLargePopup,
                showNext: () => this.ShowImageDetails(nextItem),
                showPrevious: () => this.ShowImageDetails(previousItem));

            this.SmallDialogManager.PopupDialog = dialog;
        }

        private void ShowLargePopup(ViewModelBase dialog)
        {
            this.BigDialogManager.PopupDialog = dialog;
        }

        private void CloseSmallPopupHandler()
        {
            (this.SmallDialogManager.PopupDialog as IDisposable)?.Dispose();
            this.SmallDialogManager.PopupDialog = null;
        }

        private void CloseBigPopupHandler()
        {
            (this.BigDialogManager.PopupDialog as IDisposable)?.Dispose();
            this.BigDialogManager.PopupDialog = null;
        }

        /// <summary>
        /// <see cref="AttachmentImageItem"/> represents each image that will be shown in the gallery.
        /// </summary>
        public class AttachmentImageItem : ViewModelBase
        {
            private bool isLoading;
            private Stream imageData;

            /// <summary>
            /// Initializes a new instance of the <see cref="AttachmentImageItem"/> class.
            /// </summary>
            /// <param name="url">The URL of the image.</param>
            /// <param name="message">The <see cref="Message"/> this image was sent with.</param>
            /// <param name="imageIndex">The index of this image out of all the images attached to the same <see cref="Message"/>.</param>
            /// <param name="downloader">The <see cref="GroupMeClientApi.ImageDownloader"/> that should be used to download images.</param>
            public AttachmentImageItem(string url, Message message, int imageIndex, ImageDownloader downloader)
            {
                this.Message = message;
                this.Url = url;
                this.ImageIndex = imageIndex;
                this.ImageDownloader = downloader;

                _ = this.LoadImage();
            }

            /// <summary>
            /// Gets a stream containing the image data to display.
            /// </summary>
            public Stream ImageData
            {
                get => this.imageData;
                private set => this.Set(() => this.ImageData, ref this.imageData, value);
            }

            /// <summary>
            /// Gets a value indicating whether the image is still loading.
            /// </summary>
            public bool IsLoading
            {
                get => this.isLoading;
                private set => this.Set(() => this.IsLoading, ref this.isLoading, value);
            }

            /// <summary>
            /// Gets the <see cref="GroupMeClientApi.Models.Message"/> this image was attached to.
            /// </summary>
            public Message Message { get; }

            /// <summary>
            /// Gets the index number of this image out of the collection of all images attached to a given <see cref="GroupMeClientApi.Models.Message"/>.
            /// </summary>
            public int ImageIndex { get; }

            private string Url { get; }

            private ImageDownloader ImageDownloader { get; }

            private async Task LoadImage()
            {
                this.IsLoading = true;

                var image = await this.ImageDownloader.DownloadPostImageAsync(this.Url);

                if (image == null)
                {
                    return;
                }

                this.ImageData = new MemoryStream(image);

                this.IsLoading = false;
            }
        }

        /// <summary>
        /// <see cref="ImageGalleryPlugin"/> defines a GroupMe Desktop Client Plugin that can be used
        /// to display an image gallery for a specific group or chat.
        /// </summary>
        public class ImageGalleryPlugin : PluginBase, IGroupChatPlugin
        {
            /// <inheritdoc/>
            public string PluginName => "Image Gallery New";

            /// <inheritdoc/>
            public override string PluginDisplayName => this.PluginName;

            /// <inheritdoc/>
            public override string PluginVersion => ThisAssembly.SimpleVersion;

            /// <inheritdoc/>
            public override Version ApiVersion => new Version(2, 0, 0);

            /// <inheritdoc/>
            public Task Activated(IMessageContainer groupOrChat, CacheSession cacheSession, IPluginUIIntegration integration, Action<CacheSession> cleanup)
            {
                var dataContext = new ImageGalleryWindowViewModel(groupOrChat, cacheSession, integration);
                var window = new ImageGalleryWindow
                {
                    DataContext = dataContext,
                };

                window.Closing += (s, e) =>
                {
                    cleanup(cacheSession);
                };

                Application.Current.Dispatcher.Invoke(() =>
                {
                    window.Show();
                });

                return Task.CompletedTask;
            }
        }
    }
}

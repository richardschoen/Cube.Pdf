﻿/* ------------------------------------------------------------------------- */
//
// Copyright (c) 2010 CubeSoft, Inc.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
/* ------------------------------------------------------------------------- */
using Cube.Pdf.Mixin;
using Cube.Tasks;
using Cube.Xui.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Cube.Pdf.App.Editor
{
    /* --------------------------------------------------------------------- */
    ///
    /// ImageList
    ///
    /// <summary>
    /// Provides a collection of images in which contents of Page
    /// objects are rendered.
    /// </summary>
    ///
    /* --------------------------------------------------------------------- */
    public class ImageList : IReadOnlyList<ImageEntry>, INotifyCollectionChanged
    {
        #region Constructors

        /* ----------------------------------------------------------------- */
        ///
        /// ImageCacheList
        ///
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        ///
        /// <param name="context">Synchronization context.</param>
        ///
        /* ----------------------------------------------------------------- */
        public ImageList(SynchronizationContext context)
        {
            _context  = context;
            _created  = new SortedDictionary<int, ImageSource>();
            _creating = new HashSet<int>();
            _inner    = new ObservableCollection<ImageEntry>();
            _inner.CollectionChanged += WhenCollectionChanged;

            Preferences.PropertyChanged += WhenPreferenceChanged;
        }

        #endregion

        #region Properties

        /* ----------------------------------------------------------------- */
        ///
        /// Items[int]
        ///
        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public ImageEntry this[int index] => _inner[index];

        /* ----------------------------------------------------------------- */
        ///
        /// Renderer
        ///
        /// <summary>
        /// Gets or sets the object to render Page contents.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public IDocumentRenderer Renderer { get; set; }

        /* ----------------------------------------------------------------- */
        ///
        /// Preferences
        ///
        /// <summary>
        /// Gets the preferences for images.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public ImagePreferences Preferences { get; } = new ImagePreferences();

        /* ----------------------------------------------------------------- */
        ///
        /// Count
        ///
        /// <summary>
        /// Gets the number of elements contained in this collection.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public int Count => _inner.Count;

        /* ----------------------------------------------------------------- */
        ///
        /// Loading
        ///
        /// <summary>
        /// Gets the image object representing loading.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public ImageSource Loading
        {
            get => _loading ?? (_loading = GetLoadingImage());
            set => _loading = value;
        }

        #endregion

        #region Events

        #region CollectionChanged

        /* ----------------------------------------------------------------- */
        ///
        /// CollectionChanged
        ///
        /// <summary>
        /// Occurs when an item is added, removed, changed, moved,
        /// or the entire list is refreshed.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /* ----------------------------------------------------------------- */
        ///
        /// OnCollectionChanged
        ///
        /// <summary>
        /// Raises the CollectionChanged event with the provided arguments.
        /// </summary>
        ///
        /// <param name="e">Arguments of the event being raised.</param>
        ///
        /* ----------------------------------------------------------------- */
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged == null) return;
            if (_context != null) _context.Send(_ => CollectionChanged(this, e), null);
            else CollectionChanged(this, e);
        }

        #endregion

        #endregion

        #region Methods

        /* ----------------------------------------------------------------- */
        ///
        /// GetEnumerator
        ///
        /// <summary>
        /// Returns an enumerator that iterates through this collection.
        /// </summary>
        ///
        /// <returns>
        /// An IEnumerator(ImageEntry) object for this collection.
        /// </returns>
        ///
        /* ----------------------------------------------------------------- */
        public IEnumerator<ImageEntry> GetEnumerator()
        {
            for (var i = 0; i < Count; ++i) yield return this[i];
        }

        /* ----------------------------------------------------------------- */
        ///
        /// IEnumerable.GetEnumerator
        ///
        /// <summary>
        /// Returns an enumerator that iterates through this collection.
        /// </summary>
        ///
        /// <returns>
        /// An IEnumerator object for this collection.
        /// </returns>
        ///
        /* ----------------------------------------------------------------- */
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /* ----------------------------------------------------------------- */
        ///
        /// Add
        ///
        /// <summary>
        /// Adds the Page object to be rendered.
        /// </summary>
        ///
        /// <param name="item">Page object.</param>
        ///
        /* ----------------------------------------------------------------- */
        public void Add(Page item)
        {
            Preferences.Register(item.GetDisplaySize());
            _inner.Add(new ImageEntry(e => GetImage(e), Preferences)
            {
                Text      = (_inner.Count + 1).ToString(),
                RawObject = item,
            });
        }

        /* ----------------------------------------------------------------- */
        ///
        /// Reset
        ///
        /// <summary>
        /// Resets all of images.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public void Reset()
        {
            Renderer = null;
            _inner.Clear();
            lock (_created) _created.Clear();
            lock (_creating) _creating.Clear();
        }

        /* ----------------------------------------------------------------- */
        ///
        /// Update
        ///
        /// <summary>
        /// Removes unused items and and regenerates new items.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        public void Update()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var first = Preferences.VisibleFirst;
            var last  = Math.Min(Preferences.VisibleLast, _inner.Count);

            SetImages(first, last, _cts.Token).Forget();
        }

        #endregion

        #region Implementations

        /* ----------------------------------------------------------------- */
        ///
        /// GetLoadingImage
        ///
        /// <summary>
        /// Gets a default ImageSource that notifies loading.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        private ImageSource GetLoadingImage() =>
            new BitmapImage(new Uri("pack://application:,,,/Assets/Medium/Loading.png"));

        /* ----------------------------------------------------------------- */
        ///
        /// GetImage
        ///
        /// <summary>
        /// Gets an ImageSource from the specified object.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        private ImageSource GetImage(ImageEntry src)
        {
            lock (_created)
            {
                return _created.TryGetValue(src.RawObject.Number, out var dest) ?
                       dest :
                       Loading;
            }
        }

        /* ----------------------------------------------------------------- */
        ///
        /// SetImage
        ///
        /// <summary>
        /// Stores an ImageSource to the Cache collection and raises
        /// the PropertyChanged event of the specified ImageEntry object.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        private Task SetImage(ImageEntry src) => Task.Run(() =>
        {
            var n = src.RawObject.Number;
            lock (_created) if (_created.ContainsKey(n)) return;
            lock (_creating)
            {
                if (_creating.Contains(n)) return;
                _creating.Add(n);
            }

            using (var bmp = new Bitmap(src.Width, src.Height))
            {
                using (var gs = Graphics.FromImage(bmp))
                {
                    gs.Clear(System.Drawing.Color.White);
                    Renderer.Render(gs, src.RawObject);
                }

                var dest = bmp.ToBitmapImage();
                lock (_created) if (!_created.ContainsKey(n)) _created.Add(n, dest);
                lock (_creating) _creating.Remove(n);
            }
            src.Update();
        });

        /* ----------------------------------------------------------------- */
        ///
        /// SetImages
        ///
        /// <summary>
        /// Stores ImageSource items to the Cache collection.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        private async Task SetImages(int first, int last, CancellationToken token)
        {
            try
            {
                for (var i = first; i < last; ++i)
                {
                    token.ThrowIfCancellationRequested();
                    await SetImage(_inner[i]).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { /* Ignore */ }
        }

        /* ----------------------------------------------------------------- */
        ///
        /// WhenCollectionChanged
        ///
        /// <summary>
        /// Called when the collection is changed.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        private void WhenCollectionChanged(object s, NotifyCollectionChangedEventArgs e)
        {
            Update();
            OnCollectionChanged(e);
        }

        /* ----------------------------------------------------------------- */
        ///
        /// WhenPropertyChanged
        ///
        /// <summary>
        /// Called when a property of the Preferences is changed.
        /// </summary>
        ///
        /* ----------------------------------------------------------------- */
        private void WhenPreferenceChanged(object s, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Preferences.VisibleFirst)) Update();
        }

        #endregion

        #region Fields
        private readonly SynchronizationContext _context;
        private readonly ObservableCollection<ImageEntry> _inner;
        private readonly IDictionary<int, ImageSource> _created;
        private readonly HashSet<int> _creating;
        private ImageSource _loading;
        private CancellationTokenSource _cts;
        #endregion
    }
}
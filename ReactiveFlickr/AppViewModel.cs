using ReactiveUI;
using System.Collections.Generic;
using System.Windows;
using System;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Diagnostics;
using System.Xml.Linq;
using System.Globalization;
using System.Web;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReactiveFlickr
{
    public class AppViewModel : ReactiveObject
    {
        string _SearchTerm;
        public string SearchTerm
        {
            get
            {
                return _SearchTerm;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref _SearchTerm, value);
            }
        }

        public ReactiveCommand<string, List<FlickrFoto>> ExecuteSearch { get; protected set; }

        ObservableAsPropertyHelper<List<FlickrFoto>> _SearchResults;
        public List<FlickrFoto> SearchResults => _SearchResults.Value;

        ObservableAsPropertyHelper<Visibility> _SpinnerVisibility;
        public Visibility SpinnerVisibility => _SpinnerVisibility.Value;

        public AppViewModel()
        {
            ExecuteSearch = ReactiveCommand.CreateFromTask<string, List<FlickrFoto>>(
                searchTerm => GetSearchResultsFromFlickrAsync(searchTerm)
            );

            this.WhenAnyValue(x => x.SearchTerm)
                .Throttle(TimeSpan.FromMilliseconds(800), RxApp.MainThreadScheduler)
                .Select(x => x?.Trim())
                .DistinctUntilChanged()
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .InvokeCommand(ExecuteSearch);

            _SpinnerVisibility = ExecuteSearch.IsExecuting
                .Select(x => x ? Visibility.Visible : Visibility.Collapsed)
                .ToProperty(this, x => x.SpinnerVisibility, Visibility.Hidden);

            ExecuteSearch.ThrownExceptions.Subscribe(ex => { Debug.WriteLine(ex); });

            _SearchResults = ExecuteSearch.ToProperty(this, x => x.SearchResults, new List<FlickrFoto>());
        }

        private async Task<List<FlickrFoto>> GetSearchResultsFromFlickrAsync(string searchTerm)
        {
            var doc = await Task.Run(() => XDocument.Load(string.Format(CultureInfo.InvariantCulture,
                "http://api.flickr.com/services/feeds/photos_public.gne?tags={0}&format=rss_200",
                HttpUtility.UrlEncode(searchTerm))));

            if (doc.Root == null)
                return null;

            var titles = doc.Root.Descendants("{http://search.yahoo.com/mrss/}title")
                .Select(x => x.Value);

            var tagRegex = new Regex("<[^>]+>", RegexOptions.IgnoreCase);
            var descriptions = doc.Root.Descendants("{http://search.yahoo.com/mrss/}description")
                .Select(x => tagRegex.Replace(HttpUtility.HtmlDecode(x.Value), string.Empty));

            var items = titles.Zip(descriptions, (t, d) => new FlickrFoto
            {
                Title = t,
                Description = d
            })
            .ToArray();

            var urls = doc.Root.Descendants("{http://search.yahoo.com/mrss/}thumbnail")
                .Select(x => x.Attributes("url").First().Value);

            var ret = items.Zip(urls, (item, url) => { item.Url = url; return item; }).ToList();
            return ret;
        }
    }
}
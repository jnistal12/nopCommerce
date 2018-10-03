using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Forums;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.News;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Seo;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Topics;

namespace Nop.Services.Seo
{
    /// <summary>
    /// Represents a sitemap generator
    /// </summary>
    public partial class SitemapGenerator : ISitemapGenerator
    {
        #region Fields

        private readonly BlogSettings _blogSettings;
        private readonly CommonSettings _commonSettings;
        private readonly ForumSettings _forumSettings;
        private readonly LocalizationSettings _localizationSettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICategoryService _categoryService;
        private readonly ILanguageService _languageService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IProductService _productService;
        private readonly IProductTagService _productTagService;
        private readonly IStoreContext _storeContext;
        private readonly ITopicService _topicService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly NewsSettings _newsSettings;
        private readonly SecuritySettings _securitySettings;

        #endregion

        #region Ctor

        public SitemapGenerator(BlogSettings blogSettings,
            CommonSettings commonSettings,
            ForumSettings forumSettings,
            LocalizationSettings localizationSettings,
            IActionContextAccessor actionContextAccessor,
            ICategoryService categoryService,
            ILanguageService languageService,
            IManufacturerService manufacturerService,
            IProductService productService,
            IProductTagService productTagService,
            IStoreContext storeContext,
            ITopicService topicService,
            IUrlHelperFactory urlHelperFactory,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper,
            NewsSettings newsSettings,
            SecuritySettings securitySettings)
        {
            this._blogSettings = blogSettings;
            this._commonSettings = commonSettings;
            this._forumSettings = forumSettings;
            this._localizationSettings = localizationSettings;
            this._actionContextAccessor = actionContextAccessor;
            this._categoryService = categoryService;
            this._languageService = languageService;
            this._manufacturerService = manufacturerService;
            this._productService = productService;
            this._productTagService = productTagService;
            this._storeContext = storeContext;
            this._topicService = topicService;
            this._urlHelperFactory = urlHelperFactory;
            this._urlRecordService = urlRecordService;
            this._webHelper = webHelper;
            this._newsSettings = newsSettings;
            this._securitySettings = securitySettings;
        }

        #endregion

        #region Nested class

        /// <summary>
        /// Represents sitemap URL entry
        /// </summary>
        protected class SitemapUrl
        {
            /// <summary>
            /// Ctor
            /// </summary>
            /// <param name="location">URL of the page</param>
            /// <param name="alternateLocations">List of the page urls</param>
            /// <param name="frequency">Update frequency</param>
            /// <param name="updatedOn">Updated on</param>
            public SitemapUrl(string location, IList<string> alternateLocations, UpdateFrequency frequency, DateTime updatedOn)
            {
                Location = location;
                AlternateLocations = alternateLocations;
                UpdateFrequency = frequency;
                UpdatedOn = updatedOn;
            }

            /// <summary>
            /// Ctor
            /// </summary>
            /// <param name="location">URL of the page</param>
            /// <param name="anotheUrl">The another site map url</param>
            public SitemapUrl(string location, SitemapUrl anotheUrl)
            {
                Location = location;
                AlternateLocations = anotheUrl.AlternateLocations;
                UpdateFrequency = anotheUrl.UpdateFrequency;
                UpdatedOn = anotheUrl.UpdatedOn;
            }

            /// <summary>
            /// Gets or sets URL of the page
            /// </summary>
            public string Location { get; set; }

            /// <summary>
            /// Gets or sets localized URLs of the page
            /// </summary>
            public IList<string> AlternateLocations { get; set; }

            /// <summary>
            /// Gets or sets a value indicating how frequently the page is likely to change
            /// </summary>
            public UpdateFrequency UpdateFrequency { get; set; }

            /// <summary>
            /// Gets or sets the date of last modification of the file
            /// </summary>
            public DateTime UpdatedOn { get; set; }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get UrlHelper
        /// </summary>
        /// <returns>UrlHelper</returns>
        protected virtual IUrlHelper GetUrlHelper()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
        }

        /// <summary>
        /// Get HTTP protocol
        /// </summary>
        /// <returns>Protocol name as string</returns>
        protected virtual string GetHttpProtocol()
        {
            return _securitySettings.ForceSslForAllPages ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        }

        /// <summary>
        /// Generate URLs for the sitemap
        /// </summary>
        /// <param name="langs">Language list</param>
        /// <returns>List of sitemap URLs</returns>
        protected virtual IList<SitemapUrl> GenerateUrls(IList<Language> langs = null)
        {
            var sitemapUrls = new List<SitemapUrl>
            {
                //home page
                GetLocalizedSitemapUrl("HomePage", null, langs, DateTime.UtcNow),

                //search products
                GetLocalizedSitemapUrl("ProductSearch", null, langs, DateTime.UtcNow),

                //contact us
                GetLocalizedSitemapUrl("ContactUs", null, langs, DateTime.UtcNow)
            };

            //news
            if (_newsSettings.Enabled)
            {
                sitemapUrls.Add(GetLocalizedSitemapUrl("NewsArchive", null, langs, DateTime.UtcNow));
            }

            //blog
            if (_blogSettings.Enabled)
            {
                sitemapUrls.Add(GetLocalizedSitemapUrl("Blog", null, langs, DateTime.UtcNow));
            }

            //forum
            if (_forumSettings.ForumsEnabled)
            {
                sitemapUrls.Add(GetLocalizedSitemapUrl("Boards", null, langs, DateTime.UtcNow));
            }

            //categories
            if (_commonSettings.SitemapIncludeCategories)
                sitemapUrls.AddRange(GetCategoryUrls(langs));

            //manufacturers
            if (_commonSettings.SitemapIncludeManufacturers)
                sitemapUrls.AddRange(GetManufacturerUrls(langs));

            //products
            if (_commonSettings.SitemapIncludeProducts)
                sitemapUrls.AddRange(GetProductUrls(langs));

            //product tags
            if (_commonSettings.SitemapIncludeProductTags)
                sitemapUrls.AddRange(GetProductTagUrls(langs));

            //topics
            sitemapUrls.AddRange(GetTopicUrls(langs));

            //custom URLs
            sitemapUrls.AddRange(GetCustomUrls());

            return sitemapUrls;
        }

        /// <summary>
        /// Get category URLs for the sitemap
        /// </summary>
        /// <param name="langs">Language list</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetCategoryUrls(IList<Language> langs = null)
        {
            return _categoryService.GetAllCategories(storeId: _storeContext.CurrentStore.Id)
                .Select(category => GetLocalizedSitemapUrl("Category", GetSeoRouteParams(category), langs, category.UpdatedOnUtc));
        }

        /// <summary>
        /// Get manufacturer URLs for the sitemap
        /// </summary>
        /// <param name="langs">Language list</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetManufacturerUrls(IList<Language> langs = null)
        {
            return _manufacturerService.GetAllManufacturers(storeId: _storeContext.CurrentStore.Id)
                .Select(manufacturer => GetLocalizedSitemapUrl("Manufacturer", GetSeoRouteParams(manufacturer), langs, manufacturer.UpdatedOnUtc));
        }

        /// <summary>
        /// Get product URLs for the sitemap
        /// </summary>
        /// <param name="langs">Language list</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetProductUrls(IList<Language> langs = null)
        {
            return _productService.SearchProducts(storeId: _storeContext.CurrentStore.Id,
                visibleIndividuallyOnly: true, orderBy: ProductSortingEnum.CreatedOn)
                    .Select(product => GetLocalizedSitemapUrl("Product", GetSeoRouteParams(product), langs, product.UpdatedOnUtc));
        }

        /// <summary>
        /// Get product tag URLs for the sitemap
        /// </summary>
        /// <param name="langs">Language list</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetProductTagUrls(IList<Language> langs = null)
        {
            return _productTagService.GetAllProductTags()
                .Select(productTag => GetLocalizedSitemapUrl("ProductsByTag", GetSeoRouteParams(productTag), langs, DateTime.UtcNow));
        }

        /// <summary>
        /// Get topic URLs for the sitemap
        /// </summary>
        /// <param name="langs">Language list</param>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetTopicUrls(IList<Language> langs = null)
        {
            return _topicService.GetAllTopics(_storeContext.CurrentStore.Id).Where(t => t.IncludeInSitemap)
                .Select(topic => GetLocalizedSitemapUrl("Topic", GetSeoRouteParams(topic), langs, DateTime.UtcNow));
        }

        /// <summary>
        /// Get custom URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetCustomUrls()
        {
            var storeLocation = _webHelper.GetStoreLocation();

            return _commonSettings.SitemapCustomUrls.Select(customUrl =>
                new SitemapUrl(string.Concat(storeLocation, customUrl), new List<string>(), UpdateFrequency.Weekly, DateTime.UtcNow));
        }

        /// <summary>
        /// Get route params for URL localization
        /// </summary>
        /// <typeparam name="T">Model type</typeparam>
        /// <param name="model">Model</param>
        /// <returns>Lambda for route params</returns>
        protected virtual Func<int?, object> GetSeoRouteParams<T>(T model)
            where T : BaseEntity, ISlugSupported
        {
            return lang => new { SeName = _urlRecordService.GetSeName(model, lang) };
        }

        /// <summary>
        /// Return localized urls
        /// </summary>
        /// <param name="routeName">Route name</param>
        /// <param name="routeParams">Lambda for route params object</param>
        /// <param name="languages">List of languages</param>
        /// <param name="updatedOn">A time when URL was updated last time</param>
        /// <param name="updateFreq">How often to update url</param>
        protected virtual SitemapUrl GetLocalizedSitemapUrl(string routeName, 
            Func<int?, object> routeParams, 
            IList<Language> languages, 
            DateTime updatedOn,
            UpdateFrequency updateFreq = UpdateFrequency.Weekly)
        {
            var urlHelper = GetUrlHelper();
            
            //url for current language
            var url = urlHelper.RouteUrl(routeName, routeParams?.Invoke(null), GetHttpProtocol());

            if (languages == null)
                return new SitemapUrl(url, new List<string>(), updateFreq, updatedOn);

            var pathBase = _actionContextAccessor.ActionContext.HttpContext.Request.PathBase;
            //return list of localized urls
            var localizedUrls = languages
                .Select(lang =>
                {
                    var currentUrl = urlHelper.RouteUrl(routeName, routeParams?.Invoke(lang.Id), GetHttpProtocol());

                    if (string.IsNullOrEmpty(currentUrl))
                        return null;

                    //Extract server and path from url
                    var scheme = new Uri(currentUrl).GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
                    var path = new Uri(currentUrl).PathAndQuery;

                    //Replace seo code
                    var localizedPath = path
                        .RemoveLanguageSeoCodeFromUrl(pathBase, true)
                        .AddLanguageSeoCodeToUrl(pathBase, true, lang);

                    return new Uri(new Uri(scheme), localizedPath).ToString();
                })
                .Where(value => !string.IsNullOrEmpty(value))
                .ToList();

            return new SitemapUrl(url, localizedUrls, updateFreq, updatedOn);
        }

        /// <summary>
        /// Write sitemap index file into the stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="sitemapNumber">The number of sitemaps</param>
        protected virtual void WriteSitemapIndex(Stream stream, int sitemapNumber)
        {
            var urlHelper = GetUrlHelper();

            using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("sitemapindex");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
                writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns:xhtml", "http://www.w3.org/1999/xhtml");
                writer.WriteAttributeString("xsi:schemaLocation", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                //write URLs of all available sitemaps
                for (var id = 1; id <= sitemapNumber; id++)
                {
                    var url = urlHelper.RouteUrl("sitemap-indexed.xml", new { Id = id }, GetHttpProtocol());
                    var location = XmlHelper.XmlEncode(url);

                    writer.WriteStartElement("sitemap");
                    writer.WriteElementString("loc", location);
                    writer.WriteElementString("lastmod", DateTime.UtcNow.ToString(NopSeoDefaults.SitemapDateFormat));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Write sitemap file into the stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="sitemapUrls">List of sitemap URLs</param>
        protected virtual void WriteSitemap(Stream stream, IList<SitemapUrl> sitemapUrls)
        {
            using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("urlset");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
                writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns:xhtml", "http://www.w3.org/1999/xhtml");
                writer.WriteAttributeString("xsi:schemaLocation", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                //write URLs from list to the sitemap
                foreach (var sitemapUrl in sitemapUrls)
                {
                    //write base url
                    WriteSitemapUrl(writer, sitemapUrl);

                    //write all alternate url if exists
                    foreach (var alternate in sitemapUrl.AlternateLocations
                        .Where(p => !p.Equals(sitemapUrl.Location, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        WriteSitemapUrl(writer, new SitemapUrl(alternate, sitemapUrl));
                    }
                }

                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Write sitemap
        /// </summary>
        /// <param name="writer">XML stream writer</param>
        /// <param name="sitemapUrl">Sitemap URL</param>
        protected virtual void WriteSitemapUrl(XmlTextWriter writer, SitemapUrl sitemapUrl)
        {
            writer.WriteStartElement("url");

            var loc = XmlHelper.XmlEncode(sitemapUrl.Location);
            writer.WriteElementString("loc", loc);

            //write all related url
            foreach (var alternate in sitemapUrl.AlternateLocations)
            {
                if (string.IsNullOrEmpty(alternate))
                    continue;

                //extract seo code
                var altLoc = XmlHelper.XmlEncode(alternate);
                var altLocPath = new Uri(altLoc).PathAndQuery;
                altLocPath.IsLocalizedUrl(_actionContextAccessor.ActionContext.HttpContext.Request.PathBase, true, out var lang);

                if (string.IsNullOrEmpty(lang?.UniqueSeoCode))
                    continue;

                writer.WriteStartElement("xhtml:link");
                writer.WriteAttributeString("rel", "alternate");
                writer.WriteAttributeString("hreflang", lang.UniqueSeoCode);
                writer.WriteAttributeString("href", altLoc);
                writer.WriteEndElement();
            }

            writer.WriteElementString("changefreq", sitemapUrl.UpdateFrequency.ToString().ToLowerInvariant());
            writer.WriteElementString("lastmod", sitemapUrl.UpdatedOn.ToString(NopSeoDefaults.SitemapDateFormat, CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        #endregion

        #region Methods

        /// <summary>
        /// This will build an XML sitemap for better index with search engines.
        /// See http://en.wikipedia.org/wiki/Sitemaps for more information.
        /// </summary>
        /// <param name="id">Sitemap identifier</param>
        /// <returns>Sitemap.xml as string</returns>
        public virtual string Generate(int? id)
        {
            using (var stream = new MemoryStream())
            {
                Generate(stream, id);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// This will build an XML sitemap for better index with search engines.
        /// See http://en.wikipedia.org/wiki/Sitemaps for more information.
        /// </summary>
        /// <param name="id">Sitemap identifier</param>
        /// <param name="stream">Stream of sitemap.</param>
        public virtual void Generate(Stream stream, int? id)
        {
            //generate all URLs for the sitemap
            var sitemapUrls = _localizationSettings.SeoFriendlyUrlsForLanguagesEnabled
                ? GenerateUrls(_languageService.GetAllLanguages())
                : GenerateUrls();

            //split URLs into separate lists based on the max size 
            var sitemaps = sitemapUrls
                .Select((url, index) => new { Index = index, Value = url })
                .GroupBy(group => group.Index / NopSeoDefaults.SitemapMaxUrlNumber)
                .Select(group => group
                    .Select(url => url.Value)
                    .ToList()
                ).ToList();

            if (!sitemaps.Any())
                return;

            if (id.HasValue)
            {
                //requested sitemap does not exist
                if (id.Value == 0 || id.Value > sitemaps.Count)
                    return;

                //otherwise write a certain numbered sitemap file into the stream
                WriteSitemap(stream, sitemaps.ElementAt(id.Value - 1));
            }
            else
            {
                //URLs more than the maximum allowable, so generate a sitemap index file
                if (sitemapUrls.Count >= NopSeoDefaults.SitemapMaxUrlNumber)
                {
                    //write a sitemap index file into the stream
                    WriteSitemapIndex(stream, sitemaps.Count);
                }
                else
                {
                    //otherwise generate a standard sitemap
                    WriteSitemap(stream, sitemaps.First());
                }
            }
        }

        #endregion
    }
}
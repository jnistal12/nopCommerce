using System;
using System.Net;
using System.Text;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Html;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Tax;

namespace Nop.Services.Orders
{
    /// <summary>
    /// Checkout attribute helper
    /// </summary>
    public partial class CheckoutAttributeFormatter : ICheckoutAttributeFormatter
    {
        #region Fields

        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICheckoutAttributeService _checkoutAttributeService;
        private readonly ICurrencyService _currencyService;
        private readonly IDownloadService _downloadService;
        private readonly ILocalizationService _localizationService;
        private readonly IPriceFormatter _priceFormatter;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;

        #endregion

        #region Ctor

        public CheckoutAttributeFormatter(ICheckoutAttributeParser checkoutAttributeParser,
            ICheckoutAttributeService checkoutAttributeService,
            ICurrencyService currencyService,
            IDownloadService downloadService,
            ILocalizationService localizationService,
            IPriceFormatter priceFormatter,
            ITaxService taxService,
            IWebHelper webHelper,
            IWorkContext workContext)
        {
            this._checkoutAttributeParser = checkoutAttributeParser;
            this._checkoutAttributeService = checkoutAttributeService;
            this._currencyService = currencyService;
            this._downloadService = downloadService;
            this._localizationService = localizationService;
            this._priceFormatter = priceFormatter;
            this._taxService = taxService;
            this._webHelper = webHelper;
            this._workContext = workContext;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Formats attributes
        /// </summary>
        /// <param name="attributesXml">Attributes in XML format</param>
        /// <returns>Attributes</returns>
        public virtual string FormatAttributes(string attributesXml)
        {
            var customer = _workContext.CurrentCustomer;
            return FormatAttributes(attributesXml, customer);
        }

        /// <summary>
        /// Formats attributes
        /// </summary>
        /// <param name="attributesXml">Attributes in XML format</param>
        /// <param name="customer">Customer</param>
        /// <param name="separator">Separator</param>
        /// <param name="htmlEncode">A value indicating whether to encode (HTML) values</param>
        /// <param name="renderPrices">A value indicating whether to render prices</param>
        /// <param name="allowHyperlinks">A value indicating whether to HTML hyperlink tags could be rendered (if required)</param>
        /// <returns>Attributes</returns>
        public virtual string FormatAttributes(string attributesXml,
            Customer customer,
            string separator = "<br />",
            bool htmlEncode = true,
            bool renderPrices = true,
            bool allowHyperlinks = true)
        {
            var result = new StringBuilder();

            var attributes = _checkoutAttributeParser.ParseCheckoutAttributes(attributesXml);
            for (var attributeId = 0; attributeId < attributes.Count; attributeId++)
            {
                var attribute = attributes[attributeId];
                var values = _checkoutAttributeParser.ParseValues(attributesXml, attribute.Id);
                for (var valueId = 0; valueId < values.Count; valueId++)
                {
                    var value = values[valueId];
                    var checkoutAttribute = new CheckoutAttribute();
                    if (!attribute.ShouldHaveValues())
                    {
                        switch (attribute.AttributeControlType)
                        {
                            //no values
                            case AttributeControlType.MultilineTextbox:
                            {
                                //multiline textbox
                                checkoutAttribute.Name = _localizationService.GetLocalized(attribute, a => a.Name, _workContext.WorkingLanguage.Id);
                                checkoutAttribute.Value = HtmlHelper.FormatText(value, false, true, false, false, false, false);
                                
                                //we never encode multiline textbox input
                                checkoutAttribute.DontEncodeValue = true;
                                break;
                            }
                            case AttributeControlType.FileUpload:
                            {
                                //file upload
                                Guid.TryParse(value, out var downloadGuid);
                                var download = _downloadService.GetDownloadByGuid(downloadGuid);
                                if (download != null)
                                {
                                    //TODO add a method for getting URL (use routing because it handles all SEO friendly URLs)
                                    var fileName = $"{download.Filename ?? download.DownloadGuid.ToString()}{download.Extension}";
                                    //encode (if required)
                                    if (htmlEncode)
                                        fileName = WebUtility.HtmlEncode(fileName);
                                    if (allowHyperlinks)
                                    {
                                        //hyperlinks are allowed
                                        var downloadLink = $"{_webHelper.GetStoreLocation(false)}download/getfileupload/?downloadId={download.DownloadGuid}";
                                        checkoutAttribute.Value = $"<a href=\"{downloadLink}\" class=\"fileuploadattribute\">{fileName}</a>";
                                    }
                                    else
                                    {
                                        //hyperlinks aren't allowed
                                        checkoutAttribute.Value = fileName;
                                    }

                                    checkoutAttribute.Name = _localizationService.GetLocalized(attribute, a => a.Name, _workContext.WorkingLanguage.Id);
                                    checkoutAttribute.DontEncodeValue = true;
                                }

                                break;
                            }
                            default:
                            {
                                //other attributes (textbox, datepicker)
                                checkoutAttribute.Name = _localizationService.GetLocalized(attribute, a => a.Name,
                                    _workContext.WorkingLanguage.Id);
                                checkoutAttribute.Value = value;
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (int.TryParse(value, out var attributeValueId))
                        {
                            var attributeValue = _checkoutAttributeService.GetCheckoutAttributeValueById(attributeValueId);
                            if (attributeValue != null)
                            {
                                checkoutAttribute.Name = _localizationService.GetLocalized(attribute, a => a.Name,
                                    _workContext.WorkingLanguage.Id);
                                checkoutAttribute.Value = _localizationService.GetLocalized(attributeValue, a => a.Name,
                                    _workContext.WorkingLanguage.Id);

                                if (renderPrices)
                                {
                                    var priceAdjustmentBase = _taxService.GetCheckoutAttributePrice(attributeValue, customer);
                                    var priceAdjustment = _currencyService.ConvertFromPrimaryStoreCurrency(priceAdjustmentBase, _workContext.WorkingCurrency);
                                    if (priceAdjustmentBase > 0)
                                    {
                                        var priceAdjustmentStr = _priceFormatter.FormatPrice(priceAdjustment);
                                        checkoutAttribute.PriceAdjustment = string.Format(
                                            _localizationService.GetResource("FormattedAttribute.PriceAdjustment"), 
                                            "+", priceAdjustmentStr, string.Empty);
                                    }
                                }
                            }
                        }
                    }

                    if (htmlEncode)
                    {
                        checkoutAttribute.Name = WebUtility.HtmlEncode(checkoutAttribute.Name);
                        checkoutAttribute.PriceAdjustment = WebUtility.HtmlEncode(checkoutAttribute.PriceAdjustment);

                        if (!checkoutAttribute.DontEncodeValue)
                            checkoutAttribute.Value = WebUtility.HtmlEncode(checkoutAttribute.Value);
                    }

                    var formattedAttribute = string.Format(
                        _localizationService.GetResource("Checkout.CheckoutAttributes.FormattedAttributes"),
                        checkoutAttribute.Name, checkoutAttribute.Value, checkoutAttribute.PriceAdjustment).Trim();

                    if (string.IsNullOrEmpty(formattedAttribute)) 
                        continue;

                    if (attributeId != 0 || valueId != 0)
                        result.Append(separator);
                    result.Append(formattedAttribute);
                }
            }

            return result.ToString();
        }

        #endregion

        #region Nested classes

        /// <summary>
        /// Represents a checkout attribute
        /// </summary>
        protected class CheckoutAttribute
        {
            public bool DontEncodeValue { get; set; }

            public string Name { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public string PriceAdjustment { get; set; } = string.Empty;
        }

        #endregion
    }
}
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Xml.Linq;
using Composite.Core.Caching;
using Composite.Core.Routing;
using Composite.Core.Routing.Pages;
using Composite.Data;
using Composite.Data.Types;
using Composite.Functions;
using Composite.Core.Instrumentation;
using Composite.Core.Localization;
using Composite.Core.WebClient.Renderings.Template;
using Composite.Core.Xml;
using Composite.C1Console.Security;
using Composite.Core.Configuration;

namespace Composite.Core.WebClient.Renderings.Page
{
    /// <summary>    
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class PageRenderer
    {
        private static readonly string LogTitle = typeof(PageRenderer).Name;
        private static readonly NameBasedAttributeComparer _nameBasedAttributeComparer = new NameBasedAttributeComparer();


        /// <exclude />
        public static FunctionContextContainer GetPageRenderFunctionContextContainer()
        {
            var mapper = new XEmbeddedControlMapper();

            var contextContainer = new FunctionContextContainer
            {
                XEmbedableMapper = mapper,
                SuppressXhtmlExceptions = GlobalSettingsFacade.PrettifyRenderFunctionExceptions 
                                            || PageRenderer.RenderingReason == RenderingReason.ScreenshotGeneration 
            };

            return contextContainer;
        }



        /// <exclude />
        public static Control Render(this IPage page, IEnumerable<IPagePlaceholderContent> placeholderContents, FunctionContextContainer functionContextContainer)
        {
            Verify.ArgumentNotNull(page, "page");
            Verify.ArgumentNotNull(functionContextContainer, "functionContextContainer");
            Verify.ArgumentCondition(functionContextContainer.XEmbedableMapper is XEmbeddedControlMapper,
                "functionContextContainer", $"Unknown or missing XEmbedable mapper on context container. Use {nameof(GetPageRenderFunctionContextContainer)}().");

            CurrentPage = page;

            using (GlobalInitializerFacade.CoreIsInitializedScope)
            {
                string url = PageUrls.BuildUrl(page);

                using (TimerProfilerFacade.CreateTimerProfiler(url ?? "(no url)"))
                {
                    var cultureInfo = page.DataSourceId.LocaleScope;
                    System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;
                    System.Threading.Thread.CurrentThread.CurrentUICulture = cultureInfo;

                    XEmbeddedControlMapper mapper = (XEmbeddedControlMapper)functionContextContainer.XEmbedableMapper;

                    XDocument document = TemplateInfo.GetTemplateDocument(page.TemplateId);

                    ResolvePlaceholders(document, placeholderContents);

                    Control c = Render(document, functionContextContainer, mapper, page);

                    return c;
                }
            }
        }

        /// <exclude />
        public static XhtmlDocument ParsePlaceholderContent(IPagePlaceholderContent placeholderContent)
        {
            if (string.IsNullOrEmpty(placeholderContent?.Content))
            {
                return new XhtmlDocument();
            }

            if (placeholderContent.Content.StartsWith("<html"))
            {
                try
                {
                    return XhtmlDocument.Parse(placeholderContent.Content);
                }
                catch (Exception) { }
            }

            return XhtmlDocument.Parse($"<html xmlns='{Namespaces.Xhtml}'><head/><body>{placeholderContent.Content}</body></html>");
        }

        private static void ResolvePlaceholders(XDocument document, IEnumerable<IPagePlaceholderContent> placeholderContents)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                var placeHolders = 
                    (from  placeholder in document.Descendants(RenderingElementNames.PlaceHolder)
                    let idAttribute = placeholder.Attribute(RenderingElementNames.PlaceHolderIdAttribute)
                    where idAttribute != null
                    select new { Element = placeholder, IdAttribute = idAttribute}).ToList();

                foreach (var placeholder in placeHolders)
                {
                    string placeHolderId = placeholder.IdAttribute.Value;
                    placeholder.IdAttribute.Remove();

                    IPagePlaceholderContent placeHolderContent =
                        placeholderContents.FirstOrDefault(f => f.PlaceHolderId == placeHolderId);

                    XhtmlDocument xhtmlDocument = ParsePlaceholderContent(placeHolderContent);
                    placeholder.Element.ReplaceWith(xhtmlDocument.Root);

                    //try
                    //{
                    //    placeholder.Element.Add(new XAttribute(RenderingElementNames.PlaceHolderIdAttribute, placeHolderId));
                    //}
                    //catch (Exception ex)
                    //{
                    //    throw new InvalidOperationException($"Failed to set id '{placeHolderId}' on element", ex);
                    //}
                }
            }
        }



        /// <exclude />
        public static Control Render(this IPage page, IEnumerable<IPagePlaceholderContent> placeholderContents)
        {
            return page.Render(placeholderContents, GetPageRenderFunctionContextContainer());
        }



        /// <exclude />
        public static Guid CurrentPageId
        {
            get
            {
                if (!RequestLifetimeCache.HasKey("PageRenderer.IPage"))
                {
                    return Guid.Empty;
                }

                return RequestLifetimeCache.TryGet<IPage>("PageRenderer.IPage").Id;
            }
        }


        /// <summary>
        /// Returns <value>true</value> if the page is rendered in a "Preview" mode
        /// </summary>
        public static RenderingReason RenderingReason
        {
            get
            {
                return RequestLifetimeCache.TryGet<RenderingReason>("PageRenderer.RenderingReason");
            }
            set
            {
                RequestLifetimeCache.Add("PageRenderer.RenderingReason", value);
            }
        }

        /// <exclude />
        public static IPage CurrentPage
        {
            get
            {
                if (!RequestLifetimeCache.HasKey("PageRenderer.IPage"))
                {
                    return null;
                }

                return RequestLifetimeCache.TryGet<IPage>("PageRenderer.IPage");
            }
            set
            {
                var currentValue = CurrentPage;
                if (currentValue == value)
                {
                    return;
                }

                Verify.IsNull(currentValue, "CurrentPage is already set");

                RequestLifetimeCache.Add("PageRenderer.IPage", value);
            }
        }



        /// <exclude />
        public static CultureInfo CurrentPageCulture
        {
            get
            {
                if (!RequestLifetimeCache.HasKey("PageRenderer.IPage"))
                {
                    return null;
                }

                var page = RequestLifetimeCache.TryGet<IPage>("PageRenderer.IPage");
                return page.DataSourceId.LocaleScope;
            }
        }



        /// <exclude />
        [Obsolete]
        public static IEnumerable<IData> GetCurrentPageAssociatedData(Type type)
        {
            return PageRenderer.CurrentPage.GetReferees(type);
        }



        /// <exclude />
        [Obsolete]
        public static IEnumerable<IData> GetCurrentPageAssociatedData<T>() where T : IData
        {
            return PageRenderer.CurrentPage.GetReferees(typeof(T));
        }


        internal static void ProcessPageDocument(
            XDocument document, 
            FunctionContextContainer contextContainer,
            IPage page)
        {
            using (Profiler.Measure("Executing embedded functions"))
            {
                ExecuteEmbeddedFunctions(document.Root, contextContainer);
            }

            using (Profiler.Measure("Resolving page fields"))
            {
                ResolvePageFields(document, page);
            }

            using (Profiler.Measure("Normalizing ASP.NET forms"))
            {
                NormalizeAspNetForms(document);
            }
        }

        internal static void ProcessXhtmlDocument(XhtmlDocument xhtmlDocument, IPage page)
        {
            using (Profiler.Measure("Normalizing XHTML document"))
            {
                NormalizeXhtmlDocument(xhtmlDocument);
            }

            using (Profiler.Measure("Resolving relative paths"))
            {
                ResolveRelativePaths(xhtmlDocument);
            }

            using (Profiler.Measure("Sorting <head> elements"))
            {
                PrioritizeHeadNodes(xhtmlDocument);
            }

            using (Profiler.Measure("Appending C1 meta tags"))
            {
                AppendC1MetaTags(page, xhtmlDocument);
            }

            using (Profiler.Measure("Parsing localization strings"))
            {
                LocalizationParser.Parse(xhtmlDocument);
            }
        }


        /// <exclude />
        public static Control Render(XDocument document, FunctionContextContainer contextContainer, IXElementToControlMapper mapper, IPage page)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                ProcessPageDocument(document, contextContainer, page);

                if (document.Root.Name != RenderingElementNames.Html)
                {
                    return new LiteralControl(document.ToString());
                }

                var xhtmlDocument = new XhtmlDocument(document);

                ProcessXhtmlDocument(xhtmlDocument, page);

                using (Profiler.Measure("Converting XHTML document into an ASP.NET control"))
                {
                    return xhtmlDocument.AsAspNetControl(mapper);
                }
            }
        }


        private static void PrioritizeHeadNodes(XhtmlDocument xhtmlDocument)
        {
            var prioritizedHeadNodes = new List<Tuple<int, XNode>>();
            foreach (var node in xhtmlDocument.Head.Nodes().ToList())
            {
                int p = GetHeadNodePriority(node);
                prioritizedHeadNodes.Add(new Tuple<int, XNode>(p, node));
                node.Remove();
            }
            xhtmlDocument.Head.Add(prioritizedHeadNodes.OrderBy(f => f.Item1).Select(f => f.Item2));
        }

        private static string AttributeValueLowered(this XElement element, string attributeName)
        {
            string value = (string)element.Attribute(attributeName);
            return value?.ToLowerInvariant();
        }

        private static int GetHeadNodePriority(XNode headNode)
        {
            if (headNode is XElement headElement)
            {
                if (headElement.Name.LocalName == "title") return 0;
                if (headElement.Name.LocalName == "meta")
                {
                    if (headElement.AttributeValueLowered("http-equiv") == "content-type") return 10;
                    if (headElement.Attribute("charset") != null) return 11;
                    if (headElement.Attribute("http-equiv") != null) return 11;

                    if (headElement.AttributeValueLowered("name") == "description") return 12;

                    if (headElement.Attribute("name") != null) return 20;

                    if (headElement.Attribute("property") != null) return 30;

                    return 20;
                }
                if (headElement.Name.LocalName == "link") return 30;
                if (headElement.Name.LocalName == "script") return 40;
            }

            return 100;
        }


        /// <summary>
        /// Appends the c1 meta tags to the head section. Those tag are used later on by SEO assistant.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="xhtmlDocument">The XHTML document.</param>
        public static void AppendC1MetaTags(IPage page, XhtmlDocument xhtmlDocument)
        {
            if (UserValidationFacade.IsLoggedIn())
            {
                bool emitMenuTitleMetaTag = string.IsNullOrEmpty(page.MenuTitle) == false;
                bool emitUrlMetaTag = string.IsNullOrEmpty(page.UrlTitle) == false;

                if (emitMenuTitleMetaTag || emitUrlMetaTag)
                {
                    xhtmlDocument.Head.Add(
                        new XComment("The C1.* meta tags are only emitted when you are logged in"),
                        new XElement(Namespaces.Xhtml + "link",
                            new XAttribute("rel", "schema.C1"),
                            new XAttribute("href", "http://www.composite.net/ns/c1/seoassistant")));

                    if (emitMenuTitleMetaTag)
                    {
                        xhtmlDocument.Head.Add(
                            new XElement(Namespaces.Xhtml + "meta",
                                new XAttribute("name", "C1.menutitle"),
                                new XAttribute("content", page.MenuTitle)));
                    }

                    if (emitUrlMetaTag)
                    {
                        var editPreview = PageRenderer.RenderingReason == RenderingReason.PreviewUnsavedChanges;

                        string url = PageUrls.BuildUrl(page) ?? PageUrls.BuildUrl(page, UrlKind.Internal);

                        var pageUrl = string.Format("{0}{1}{2}",
                            url.Replace("/c1mode(unpublished)", "").Replace("/c1mode(relative)",""),
                            editPreview ? "/" + page.UrlTitle : C1PageRoute.GetPathInfo(),
                            editPreview ? "" : HttpContext.Current.Request.Url.Query);

                        xhtmlDocument.Head.Add(
                            new XElement(Namespaces.Xhtml + "meta",
                                new XAttribute("name", "C1.urlseowords"),
                                new XAttribute("content", pageUrl)));
                    }
                }
            }
        }


        /// <exclude />
        public static void ResolveRelativePaths(XhtmlDocument xhtmlDocument)
        {
            IEnumerable<XElement> xhtmlElements = xhtmlDocument.Descendants().Where(f => f.Name.Namespace == Namespaces.Xhtml);
            IEnumerable<XAttribute> pathAttributes = xhtmlElements.Attributes().Where(f => f.Name.LocalName == "src" || f.Name.LocalName == "href" || f.Name.LocalName == "action");

            string applicationVirtualPath = UrlUtils.PublicRootPath;

            List<XAttribute> relativePathAttributes = pathAttributes.Where(f => f.Value.StartsWith("~/") || f.Value.StartsWith("%7E/")).ToList();

            foreach (XAttribute relativePathAttribute in relativePathAttributes)
            {
                int tildePrefixLength = (relativePathAttribute.Value.StartsWith("~") ? 1 : 3);
                relativePathAttribute.Value = applicationVirtualPath + relativePathAttribute.Value.Substring(tildePrefixLength);
            }

            if (applicationVirtualPath.Length > 1)
            {
                List<XAttribute> hardRootedPathAttributes = pathAttributes.Where(f => f.Value.StartsWith("/Renderers/")).ToList();

                foreach (XAttribute hardRootedPathAttribute in hardRootedPathAttributes)
                {
                    hardRootedPathAttribute.Value = applicationVirtualPath + hardRootedPathAttribute.Value;
                }
            }
        }



        private static void NormalizeAspNetForms(XDocument document)
        {
            var aspNetFormXName = Namespaces.AspNetControls + "form";
            List<XElement> aspNetFormElements = document.Descendants(aspNetFormXName).Reverse().ToList();

            foreach (XElement aspNetFormElement in aspNetFormElements)
            {
                if (aspNetFormElement.Ancestors(aspNetFormXName).Any())
                {
                    aspNetFormElement.ReplaceWith(aspNetFormElement.Nodes());
                }
            }

        }


        /// <exclude />
        public static void ResolvePageFields(XDocument document, IPage page)
        {
            foreach (XElement elem in document.Descendants(RenderingElementNames.PageTitle).ToList())
            {
                elem.ReplaceWith(page.Title);
            }

            foreach (XElement elem in document.Descendants(RenderingElementNames.PageAbstract).ToList())
            {
                elem.ReplaceWith(page.Description);
            }


            foreach (XElement elem in document.Descendants(RenderingElementNames.PageMetaTagDescription).ToList())
            {
                if (string.IsNullOrEmpty(page.Description))
                {
                    elem.Remove();
                    continue;
                }

                elem.ReplaceWith(new XElement(Namespaces.Xhtml + "meta",
                                    new XAttribute("name", "description"),
                                    new XAttribute("content", page.Description)));
            }
        }



        /// <exclude />
        public static void ExecuteEmbeddedFunctions(XElement element, FunctionContextContainer contextContainer)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                IEnumerable<XElement> functionCallDefinitions = element.DescendantsAndSelf(Namespaces.Function10 + "function")
                                                                       .Where(f => !f.Ancestors(Namespaces.Function10 + "function").Any());

                var functionCalls = functionCallDefinitions.ToList();
                if (functionCalls.Count == 0) return;

                object[] functionExecutionResults = new object[functionCalls.Count];

                for (int i = 0; i < functionCalls.Count; i++)
                {
                    XElement functionCallDefinition = functionCalls[i];
                    string functionName = null;

                    object functionResult;
                    try
                    {
                        // Evaluating function calls in parameters
                        IEnumerable<XElement> parameters = functionCallDefinition.Elements();

                        foreach (XElement parameterNode in parameters)
                        {
                            ExecuteEmbeddedFunctions(parameterNode, contextContainer);
                        }


                        // Executing a function call
                        BaseRuntimeTreeNode runtimeTreeNode = FunctionTreeBuilder.Build(functionCallDefinition);

                        functionName = runtimeTreeNode.GetAllSubFunctionNames().FirstOrDefault();

                        object result = runtimeTreeNode.GetValue(contextContainer);

                        if (result != null)
                        {
                            // Evaluating functions in a result of a function call
                            object embedableResult = contextContainer.MakeXEmbedable(result);

                            foreach (XElement xelement in GetXElements(embedableResult))
                            {
                                ExecuteEmbeddedFunctions(xelement, contextContainer);
                            }

                            functionResult = embedableResult;
                        }
                        else
                        {
                            functionResult = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        using (Profiler.Measure("PageRenderer. Loggin an exception"))
                        {
                            XElement errorBoxHtml;

                            if (!contextContainer.ProcessException(functionName, ex, LogTitle, out errorBoxHtml))
                            {
                                throw;
                            }

                            functionResult = errorBoxHtml;
                        }
                    }

                    functionExecutionResults[i] = functionResult;
                };

                // Applying changes
                for (int i = 0; i < functionCalls.Count; i++)
                {
                    XElement functionCall = functionCalls[i];
                    object functionCallResult = functionExecutionResults[i];
                    if (functionCallResult != null)
                    {
                        if (functionCallResult is XAttribute && functionCall.Parent != null)
                        {
                            functionCall.Parent.Add(functionCallResult);
                            functionCall.Remove();
                        }
                        else
                        {
                            functionCall.ReplaceWith(functionCallResult);
                        }
                    }
                    else
                    {
                        functionCall.Remove();
                    }
                }
            }
        }



        private static IEnumerable<XElement> GetXElements(object source)
        {
            if (source is XElement element)
            {
                yield return element;
            }

            if (source is IEnumerable<XNode> nodes)
            {
                foreach (var xElement in nodes.OfType<XElement>())
                {
                    yield return xElement;
                }
            }
        }



        private class NameBasedAttributeComparer : IEqualityComparer<XAttribute>
        {
            public bool Equals(XAttribute x, XAttribute y)
            {
                return x.Name == y.Name;
            }

            public int GetHashCode(XAttribute obj)
            {
                return obj.Name.GetHashCode();
            }
        }

        /// <exclude />
        public static void NormalizeXhtmlDocument(XhtmlDocument rootDocument)
        {
            using (TimerProfilerFacade.CreateTimerProfiler())
            {
                while (true)
                {
                    XElement nestedDocument = rootDocument.Root.Descendants(XhtmlDocument.XName_html).FirstOrDefault();

                    if (nestedDocument == null) break;
                    
                    var nestedHead = nestedDocument.Element(XhtmlDocument.XName_head);
                    var nestedBody = nestedDocument.Element(XhtmlDocument.XName_body);

                    Verify.IsNotNull(nestedHead, "XHTML document is missing <head /> element");
                    Verify.IsNotNull(nestedBody, "XHTML document is missing <body /> element");

                    rootDocument.Root.Add(nestedDocument.Attributes().Except(rootDocument.Root.Attributes(), _nameBasedAttributeComparer));

                    // making <meta property="..." /> from nested documents appear first. We will not filter them later and this ensure desired precedence 
                    bool IsMetaProperty(XElement e) => e.Name.LocalName == "meta" && e.Attribute("property") != null;

                    rootDocument.Head.AddFirst(nestedHead.Elements().Where(IsMetaProperty));
                    rootDocument.Head.Add(nestedHead.Nodes().Where(f => !(f is XElement e && IsMetaProperty(e))));
                    rootDocument.Head.Add(nestedHead.Attributes().Except(rootDocument.Head.Attributes(), _nameBasedAttributeComparer));
                    rootDocument.Body.Add(nestedBody.Attributes().Except(rootDocument.Body.Attributes(), _nameBasedAttributeComparer));

                    nestedDocument.ReplaceWith(nestedBody.Nodes());
                }
            }
        }



        /// <exclude />
        public static bool DisableAspNetPostback(Control c)
        {
            bool formDisabled;
            DisableAspNetPostback(c, out formDisabled);
            return formDisabled;
        }


        private static void DisableAspNetPostback(Control c, out bool formDisabled)
        {
            formDisabled = false;

            if (c is HtmlForm form)
            {
                form.Attributes.Add("onsubmit", "alert('Postback disabled in preview mode'); return false;");
                formDisabled = true;
                return;
            }

            if (c is HtmlHead)
            {
                return;
            }

            foreach (Control child in c.Controls)
            {
                DisableAspNetPostback(child, out formDisabled);
                if (formDisabled) break;
            }
        }
    }
}

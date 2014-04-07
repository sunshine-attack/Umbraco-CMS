using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Microsoft.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Web.Models;

namespace Umbraco.Web.Mvc
{
    /// <summary>
    ///     A view engine to look into the template location specified in the config for the front-end/Rendering part of the
    ///     cms,
    ///     this includes paths to render partial macros and media item templates.
    /// </summary>
    public class CustomRenderViewEngine : FixedRazorViewEngine
    {
        private readonly IEnumerable<string> _supplementedViewLocations = new[] {"/{0}.cshtml"};
        //NOTE: we will make the main view location the last to be searched since if it is the first to be searched and there is both a view and a partial
        // view in both locations and the main view is rendering a partial view with the same name, we will get a stack overflow exception. 
        // http://issues.umbraco.org/issue/U4-1287, http://issues.umbraco.org/issue/U4-1215
        private readonly IEnumerable<string> _supplementedPartialViewLocations = new[]
        {"/Partials/{0}.cshtml", "/MacroPartials/{0}.cshtml", "/{0}.cshtml"};


        private readonly IEnumerable<string> _supplementedAreaViewLocations = new[]
        {
            "~/Areas/{2}/CustomViews/{0}/{0}.cshtml",
            "~/Areas/{2}/CustomViews/{0}/{0}.vbhtml",
            "~/Areas/{2}/CustomViews/Shared/{0}.cshtml",
            "~/Areas/{2}/CustomViews/Shared/{0}.vbhtml"
        };

        private readonly IEnumerable<string> _supplementedViewLocationsPart2 = new[]
        {
                "~/CustomViews/{0}/{0}.cshtml",
                "~/CustomViews/{0}/{0}.vbhtml",
                "~/CustomViews/Shared/{0}.cshtml",
                "~/CustomViews/Shared/{0}.vbhtml"
            
        };


        /// <summary>
        ///     Constructor
        /// </summary>
        public CustomRenderViewEngine()
        {
            const string templateFolder = Constants.ViewLocation;

            string[] replaceWithUmbracoFolder = _supplementedViewLocations.ForEach(location => templateFolder + location);
            string[] replacePartialWithUmbracoFolder =
                _supplementedPartialViewLocations.ForEach(location => templateFolder + location);

            //The Render view engine doesn't support Area's so make those blank
            ViewLocationFormats = replaceWithUmbracoFolder.ToArray();
            PartialViewLocationFormats = replacePartialWithUmbracoFolder.ToArray();

            AreaPartialViewLocationFormats =
                _supplementedAreaViewLocations.ForEach(location => location).ToArray();

            AreaMasterLocationFormats =
                _supplementedAreaViewLocations.ForEach(location => location)
                    .Union(
                        _supplementedViewLocations.ForEach(location => templateFolder + location
                            )
                    ).ToArray();

            AreaViewLocationFormats = 
                _supplementedAreaViewLocations.ForEach(location => location).ToArray();

            ViewLocationFormats = _supplementedViewLocations.ForEach(location => templateFolder + location)
                .Union(_supplementedViewLocationsPart2).ToArray();

            MasterLocationFormats = _supplementedViewLocations.ForEach(location => templateFolder + location)
                .Union(_supplementedViewLocationsPart2).ToArray();

            EnsureFoldersAndFiles();

        }

        /// <summary>
        ///     Ensures that the correct web.config for razor exists in the /Views folder, the partials folder exist and the
        ///     ViewStartPage exists.
        /// </summary>
        private void EnsureFoldersAndFiles()
        {
            string viewFolder = IOHelper.MapPath(Constants.ViewLocation);
            //ensure the web.config file is in the ~/Views folder
            Directory.CreateDirectory(viewFolder);
            if (!File.Exists(Path.Combine(viewFolder, "web.config")))
            {
                using (StreamWriter writer = File.CreateText(Path.Combine(viewFolder, "web.config")))
                {
                    writer.Write(Strings.WebConfigTemplate);
                }
            }
            //auto create the partials folder
            string partialsFolder = Path.Combine(viewFolder, "Partials");
            Directory.CreateDirectory(partialsFolder);

            //We could create a _ViewStart page if it isn't there as well, but we may not allow editing of this page in the back office.
        }

        public override ViewEngineResult FindView(ControllerContext controllerContext, string viewName,
            string masterName, bool useCache)
        {
            if (!ShouldFindView(controllerContext, false))
            {
                return new ViewEngineResult(new string[] {});
            }

            return base.FindView(controllerContext, viewName, masterName, useCache);
        }

        public override ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName,
            bool useCache)
        {
            if (!ShouldFindView(controllerContext, true))
            {
                return new ViewEngineResult(new string[] {});
            }

            return base.FindPartialView(controllerContext, partialViewName, useCache);
        }

        /// <summary>
        ///     Determines if the view should be found, this is used for view lookup performance and also to ensure
        ///     less overlap with other user's view engines. This will return true if the Umbraco back office is rendering
        ///     and its a partial view or if the umbraco front-end is rendering but nothing else.
        /// </summary>
        /// <param name="controllerContext"></param>
        /// <param name="isPartial"></param>
        /// <returns></returns>
        private bool ShouldFindView(ControllerContext controllerContext, bool isPartial)
        {
            object umbracoToken = controllerContext.GetDataTokenInViewContextHierarchy("umbraco");

            //first check if we're rendering a partial view for the back office, or surface controller, etc...
            //anything that is not IUmbracoRenderModel as this should only pertain to Umbraco views.
            if (isPartial && umbracoToken is RenderModel)
            {
                return true;
            }

            //only find views if we're rendering the umbraco front end
            if (umbracoToken is RenderModel)
            {
                return true;
            }

            return false;
        }
    }
}
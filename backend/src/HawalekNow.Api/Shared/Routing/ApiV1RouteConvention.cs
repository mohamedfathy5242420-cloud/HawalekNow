using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace HawalekNow.Api.Shared.Routing;

public sealed class ApiV1RouteConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        var prefix = new AttributeRouteModel(new RouteAttribute("api/v1"));
        foreach (var controller in application.Controllers)
        {
            foreach (var selector in controller.Selectors.Concat(controller.Actions.SelectMany(action => action.Selectors)))
            {
                if (selector.AttributeRouteModel?.IsAbsoluteTemplate == true)
                    throw new InvalidOperationException("API controller routes must be relative to api/v1.");
            }
            foreach (var selector in controller.Selectors)
                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(prefix, selector.AttributeRouteModel);
        }
    }
}

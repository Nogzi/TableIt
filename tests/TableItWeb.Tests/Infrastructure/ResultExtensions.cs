using Microsoft.AspNetCore.Mvc;

namespace TableItWeb.Tests.Infrastructure;

public static class ResultExtensions
{
    /// <summary>Returns the value of an ActionResult&lt;T&gt;, whether returned implicitly or wrapped in an ObjectResult.</summary>
    public static T ValueOrFail<T>(this ActionResult<T> result)
    {
        if (result.Value is not null) return result.Value;
        var obj = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        return Assert.IsType<T>(obj.Value);
    }

    public static void AssertBadRequest<T>(this ActionResult<T> result)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, bad.StatusCode);
        Assert.IsType<string>(bad.Value);
    }
}

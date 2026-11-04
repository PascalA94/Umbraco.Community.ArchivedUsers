using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.ArchivedUsers.Web;

namespace Umbraco.Community.ArchivedUsers.Tests;

/// <summary>S1 [Critical]: every package controller is a secured Management API controller (T14).</summary>
public class ControllerConventionTests
{
    private static IEnumerable<Type> Controllers()
        => typeof(ArchivedUsersComposer).Assembly.GetTypes().Where(t => t.Name.EndsWith("Controller", StringComparison.Ordinal) && !t.IsAbstract);

    [Fact]
    public void Every_controller_is_a_secured_management_api_controller()
    {
        var controllers = Controllers().ToList();
        Assert.NotEmpty(controllers);

        foreach (Type controller in controllers)
        {
            Assert.True(typeof(ManagementApiControllerBase).IsAssignableFrom(controller), $"{controller.Name} must inherit ManagementApiControllerBase");
            Assert.Contains(
                controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true),
                a => a.Policy == AuthorizationPolicies.SectionAccessUsers);
            Assert.Equal("Archived Users", controller.GetCustomAttribute<ApiExplorerSettingsAttribute>(inherit: true)?.GroupName);
            Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
            Assert.DoesNotContain(
                controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
        }
    }

    /// <summary>AU-14: request bodies are JSON only, so a form post gets 415.</summary>
    [Fact]
    public void Every_post_action_reads_a_json_body()
    {
        var posts = Controllers()
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToList();
        Assert.NotEmpty(posts);

        foreach (MethodInfo post in posts)
        {
            // [FromBody] is what makes a form post a 415 (proved over HTTP by Form_encoded_post_returns_415).
            Assert.Contains(post.GetParameters(), p => p.GetCustomAttribute<FromBodyAttribute>() is not null);
            Assert.DoesNotContain(post.GetParameters(), p => p.GetCustomAttribute<FromFormAttribute>() is not null || p.GetCustomAttribute<FromQueryAttribute>() is not null);
        }
    }
}

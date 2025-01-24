using Azure.Identity;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using System.Reflection;
namespace B2CUserAdmin.Web.Services;

public class B2CUsersService
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly IConfiguration _configuration;

    public B2CUsersService(IConfiguration configuration)
    {
        // The client credentials flow requires that you request the
        // /.default scope, and pre-configure your permissions on the
        // app registration in Azure. An administrator must grant consent
        // to those permissions beforehand.
        var scopes = new[] { "https://graph.microsoft.com/.default" };

        // using Azure.Identity;
        var options = new ClientSecretCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
        };

        var tenantId = configuration["AzureAdB2C:TenantId"];
        var clientId = configuration["AzureAdB2C:ClientId"];
        var clientSecret = configuration["AzureAdB2C:ClientSecret"];

        // https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
        var clientSecretCredential = new ClientSecretCredential(
            tenantId, clientId, clientSecret, options);

        _graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);
        _configuration = configuration;

    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        var users = await _graphServiceClient.Users
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = ["displayName", "id", "identities", "otherMails"];

            });

        return users.Value;
    }

    public async Task<User> GetUserAsync(string? userId)
    {
        return await _graphServiceClient.Users[userId.ToString()]
            .GetAsync();
    }

    public async Task<User> GetUserAllProperties(string userId)
    {
        var b2cExtensionsAppObjectId = await Getb2cExtensionsAppObjectIdAsync();
        
        #region ChatGPT
        // try
        // {
        //     // Directly call GetAsync without .Request()
        //     var applicationExtensionProperties = GetB2CCustomExtensionAttributes(b2cExtensionsAppObjectId);

        //     // var customAttributes = new System.Collections.Generic.List<string>();

        //     // Display extension properties
        //     if (applicationExtensionProperties != null)
        //     {
        //         foreach (var extensionProperty in applicationExtensionProperties)
        //         {
        //             Console.WriteLine($"Property -name: {extensionProperty.Name} -id: {extensionProperty.Id} -dataType: {extensionProperty.DataType}");
        //             // customAttributes = extensionProperty.Name.ToList();
        //         }

        //         // var customAttributesArray = customAttributes.Select(attr => attr.Id).ToArray();
        //     }
        //     else
        //     {
        //         Console.WriteLine("No extension properties found.");
        //     }
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine($"Error retrieving application: {ex.Message}");
        // }
        #endregion

        #region User flows
        // Doesn't work because the Identity User Flow attributes does not include the B2C custom attributes

        // var customAttributes = await GetCustomExtensionAttributes();
        var customAttributes = await GetB2CCustomExtensionAttributes(b2cExtensionsAppObjectId);

        var baseProperties = new[]
        {
            "identities", "displayName", "givenName", "surname", "userPrincipalName", "accountEnabled",
            "ageGroup", "consentProvidedForMinor", "country", "creationType", "department",
            "employeeId", "faxNumber", "legalAgeGroupClassification", "mail",
            "mobilePhone", "officeLocation", "otherMails", "passwordPolicies", "postalCode",
            "preferredDataLocation", "preferredLanguage", "proxyAddresses", "showInAddressList",
            "state", "streetAddress", "city", "zipCode", "usageLocation", "id", "userType", "jobTitle", "companyName", "employeeType","businessPhone","mobilePhone"
        };

        // var customAttributesArray = customAttributes.Select(attr => attr.Id).ToArray();
        var customAttributesB2CArray = customAttributes.Select(attr => attr.Name).ToArray();
        // var customAttributesArray = new[]
        // {
        //     "extension_e56598d21fdb4d56a12c769745dafc74_lastSignInDateTime",
        //     "extension_e56598d21fdb4d56a12c769745dafc74_IdentityServerValidation",
        //     "extension_e56598d21fdb4d56a12c769745dafc74_isEmailUser"
        // };
        
        var allProperties = baseProperties.Concat(customAttributesB2CArray).ToArray();
        #endregion

        var b2cProperties = new [] 
            {
                "displayName", "id", "givenName", "surname", "userPrincipalName", "accountEnabled",
                "createdDateTime", "signInSessionsValidFromDateTime ", "otherMails", "city", "userType"
            };

        // var allProperties = b2cProperties.Concat(customb2cAttributesArray).ToArray();

        // Make a call to Microsoft Graph to get the user with the specified properties.
        var user = await _graphServiceClient.Users[userId.ToString()]
            .GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = allProperties;
                // requestConfiguration.QueryParameters.Select = b2cProperties;

            });

        return user;
    }

    public async Task UpdateUserAsync(string userId, User updatedUser)
    {
        await _graphServiceClient.Users[userId.ToString()]
            .PatchAsync(updatedUser);
    }

    public async Task<List<ExtensionProperty>> GetB2CCustomExtensionAttributes(string b2cExtensionsAppObjectId)
    {
        var b2cExtensionProperties = await _graphServiceClient.Applications[b2cExtensionsAppObjectId].ExtensionProperties
                .GetAsync();
        return b2cExtensionProperties.Value.Where(x => x.Name.StartsWith("extension_")).ToList();
    }

    public async Task<List<IdentityUserFlowAttribute>> GetCustomExtensionAttributes()
    {
        var extensionProperties = await _graphServiceClient.Identity.UserFlowAttributes.GetAsync();
        return extensionProperties.Value.Where(x => x.Id.StartsWith("extension_")).ToList();
    }

    public async Task<string> Getb2cExtensionsAppObjectIdAsync()
    {
        try
        {
            var apps = await _graphServiceClient.Applications.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Filter = "startswith(displayName, 'b2c-extensions-app')";
            });

            return apps.Value.FirstOrDefault().Id;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public string GetVersion()
    {
        var version = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        return version;
    }

}
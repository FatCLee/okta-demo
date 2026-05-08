using System.Net.Http.Headers;
using System.Net.Http.Json;
using api.Configuration;
using Microsoft.Extensions.Options;

namespace api.Infrastructure;

public sealed class OktaManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly OktaManagementOptions _options;

    public OktaManagementClient(HttpClient httpClient, IOptions<OktaManagementOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("SSWS", _options.ApiToken);
        }
    }

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_options.BaseUrl) &&
        !string.IsNullOrWhiteSpace(_options.ApiToken);

    public async Task<OktaActivationResult> CreateUserAndActivationAsync(
        string email,
        string clientName,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var (firstName, lastName) = SplitName(clientName);

        var createResponse = await _httpClient.PostAsJsonAsync(
            $"api/v1/users?activate=false",
            new
            {
                profile = new
                {
                    firstName,
                    lastName,
                    email,
                    login = email
                }
            },
            cancellationToken);

        var createPayload = await createResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!createResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Okta user creation failed: {(int)createResponse.StatusCode} {createPayload}");
        }

        var createdUser = System.Text.Json.JsonDocument.Parse(createPayload);
        var userId = createdUser.RootElement.GetProperty("id").GetString();

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("Okta user creation did not return a user id.");
        }

        var activationResponse = await _httpClient.PostAsync(
            $"api/v1/users/{userId}/lifecycle/activate?sendEmail=false",
            content: null,
            cancellationToken);

        var activationPayload = await activationResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!activationResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Okta activation failed: {(int)activationResponse.StatusCode} {activationPayload}");
        }

        var activationDocument = System.Text.Json.JsonDocument.Parse(activationPayload);
        var activationToken = activationDocument.RootElement.GetProperty("activationToken").GetString();
        var activationUrl = activationDocument.RootElement.GetProperty("activationUrl").GetString();

        if (string.IsNullOrWhiteSpace(activationToken) || string.IsNullOrWhiteSpace(activationUrl))
        {
            throw new InvalidOperationException("Okta activation response did not contain activation details.");
        }

        return new OktaActivationResult(userId, activationToken, activationUrl);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured())
        {
            throw new InvalidOperationException(
                "Okta management API is not configured. Set OktaManagement:BaseUrl and OktaManagement:ApiToken.");
        }
    }

    private static (string FirstName, string LastName) SplitName(string clientName)
    {
        var parts = clientName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return ("Client", "User");
        }

        if (parts.Length == 1)
        {
            return (parts[0], "User");
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }
}

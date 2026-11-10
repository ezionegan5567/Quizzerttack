using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    const string ProjectId = "5906c36b-06d8-410b-91be-cf367616022a";
    static readonly HttpClient Http = new HttpClient();
    static string Token;

    static string Recorta(string s, int n = 700) => s == null ? "<null>" : (s.Length > n ? s.Substring(0, n) + "..." : s);

    static async Task<string> PedirToken()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://services.api.unity.com/auth/v1/token-exchange?projectId={ProjectId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UnityServices", "credentials")).Trim());
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var res = await Http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) { Console.WriteLine("TOKEN " + (int)res.StatusCode + " " + Recorta(body)); return null; }
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("accessToken").GetString();
    }

    static async Task Get(string etiqueta, string url)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var res = await Http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        Console.WriteLine($"\n[{etiqueta}] {(int)res.StatusCode} {res.StatusCode}");
        Console.WriteLine("  " + Recorta(body));
    }

    static async Task Main()
    {
        Token = await PedirToken();
        if (Token == null) return;
        var b = "https://services.api.unity.com/remote-config/v1/projects/" + ProjectId;

        await Get("environments", b + "/environments");
        await Get("gamemodifiers", b + "/gamemodifiers");
        await Get("game-overrides", b + "/game-overrides");
        await Get("overrides", b + "/overrides");
        await Get("broadcast-overrides", b + "/broadcast-overrides");
        await Get("configs", b + "/configs");
    }
}

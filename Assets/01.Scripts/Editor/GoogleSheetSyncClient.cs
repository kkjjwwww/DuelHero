using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Editor-only: no authentication code or credentials are included in game builds.
public static class GoogleSheetSyncClient
{
    public const string ReadOnlyScope = "https://www.googleapis.com/auth/spreadsheets.readonly";
    public static readonly string[] TabNames = { "Card Data", "Card Effect Data", "Keyword Data" };
    public static readonly string[] CsvNames = { "Cards.csv", "Effects.csv", "Keywords.csv" };
    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

    [Serializable] private class ClientSettings { public string clientId, clientSecret; }
    [Serializable] private class TokenState { public string accessToken, refreshToken; public long expiresAt; }

    public static string LocalSettingsFolder
    {
        get
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            using var sha = SHA256.Create();
            string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(project))).Replace("-", "").Substring(0, 16);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DuelHero", "SheetSync", key);
        }
    }
    public static bool HasClient => File.Exists(Path.Combine(LocalSettingsFolder, "client.dat"));
    public static bool HasToken => File.Exists(Path.Combine(LocalSettingsFolder, "token.dat"));

    public static void ImportDesktopClient(string json)
    {
        var installed = JObject.Parse(json)["installed"] as JObject;
        if (installed == null) throw new FormatException("데스크톱 앱 유형의 OAuth 클라이언트 JSON이 필요합니다. 웹 앱/서비스 계정 JSON은 사용할 수 없습니다.");
        var settings = new ClientSettings { clientId = (string)installed["client_id"], clientSecret = (string)installed["client_secret"] };
        if (string.IsNullOrWhiteSpace(settings.clientId) || !settings.clientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
            throw new FormatException("OAuth 클라이언트 ID를 확인해주세요.");
        if (string.IsNullOrEmpty(settings.clientSecret)) throw new FormatException("OAuth JSON에 client_secret이 없습니다.");
        SaveProtected("client.dat", JsonUtility.ToJson(settings));
        ForgetToken();
    }

    public static void ForgetToken()
    {
        string path = Path.Combine(LocalSettingsFolder, "token.dat");
        if (File.Exists(path)) File.Delete(path);
    }

    public static async Task SignInAsync(CancellationToken cancellation)
    {
        var settings = ReadClient();
        string verifier = RandomUrlToken(48), state = RandomUrlToken(32);
        string challenge;
        using (var sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
        var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start(); int port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
        string redirect = "http://127.0.0.1:" + port + "/";
        using var listener = new HttpListener(); listener.Prefixes.Add(redirect); listener.Start();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var registration = timeout.Token.Register(() => listener.Stop());
        string url = "https://accounts.google.com/o/oauth2/v2/auth?" + Encode(new Dictionary<string, string>
        {
            ["client_id"] = settings.clientId, ["redirect_uri"] = redirect, ["response_type"] = "code",
            ["scope"] = ReadOnlyScope, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
            ["state"] = state, ["access_type"] = "offline", ["prompt"] = "consent"
        });
        Application.OpenURL(url);
        string code = null;
        while (code == null)
        {
            HttpListenerContext callback;
            try { callback = await listener.GetContextAsync(); }
            catch (Exception) when (timeout.IsCancellationRequested) { throw new OperationCanceledException("로그인이 취소되었거나 3분 제한 시간이 지났습니다.", timeout.Token); }
            if (callback.Request.Url.AbsolutePath != "/" || callback.Request.QueryString["state"] != state)
            {
                Reply(callback, 400, "Invalid OAuth callback."); continue;
            }
            if (!string.IsNullOrEmpty(callback.Request.QueryString["error"]))
            {
                Reply(callback, 403, "Authorization was not granted. Return to Unity.");
                throw new InvalidOperationException("Google 읽기 권한 승인이 취소되었습니다.");
            }
            code = callback.Request.QueryString["code"];
            Reply(callback, code == null ? 400 : 200, code == null ? "Missing authorization code." : "Authorization received. You can close this tab and return to Unity.");
        }
        var fields = new Dictionary<string, string>
        {
            ["client_id"] = settings.clientId, ["client_secret"] = settings.clientSecret,
            ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code"
        };
        TokenState token = await ExchangeAsync(fields, null, cancellation);
        if (string.IsNullOrWhiteSpace(token.refreshToken)) throw new InvalidOperationException("갱신 토큰을 받지 못했습니다. Google 권한 승인을 다시 진행해주세요.");
        SaveProtected("token.dat", JsonUtility.ToJson(token));
    }

    public static async Task<Dictionary<string, string>> DownloadCsvAsync(string spreadsheetId, CancellationToken cancellation)
    {
        if (!Regex.IsMatch(spreadsheetId ?? "", @"^[A-Za-z0-9_-]+$")) throw new ArgumentException("스프레드시트 ID 형식이 올바르지 않습니다.");
        string token = await GetAccessTokenAsync(cancellation);
        string prefix = "https://sheets.googleapis.com/v4/spreadsheets/" + spreadsheetId;
        JObject metadata = await GetJsonAsync(prefix + "?fields=" + Uri.EscapeDataString("sheets(properties(title,gridProperties(rowCount,columnCount)))"), token, cancellation);
        var ranges = new List<string>();
        foreach (string name in TabNames)
        {
            var properties = metadata["sheets"]?.Children().Select(s => s["properties"]).FirstOrDefault(p => (string)p?["title"] == name);
            if (properties == null) throw new FormatException("시트 탭을 찾을 수 없습니다: " + name);
            int rows = (int?)properties["gridProperties"]?["rowCount"] ?? 0;
            int columns = (int?)properties["gridProperties"]?["columnCount"] ?? 0;
            if (rows < 1 || columns < 1) throw new FormatException(name + ": 시트 크기를 확인할 수 없습니다.");
            ranges.Add("'" + name.Replace("'", "''") + "'!A1:" + ColumnName(columns) + rows);
        }
        string query = string.Join("&", ranges.Select(r => "ranges=" + Uri.EscapeDataString(r))) + "&majorDimension=ROWS&valueRenderOption=FORMATTED_VALUE";
        JObject payload = await GetJsonAsync(prefix + "/values:batchGet?" + query, token, cancellation);
        return ConvertValuesResponse(payload);
    }

    // Google returns ranges in request order. Preserve headers and skip id-less checkbox rows.
    public static Dictionary<string, string> ConvertValuesResponse(JObject payload)
    {
        var valueRanges = payload["valueRanges"] as JArray;
        if (valueRanges == null || valueRanges.Count != TabNames.Length) throw new FormatException("카드/효과/키워드 응답 개수가 올바르지 않습니다.");
        var files = new Dictionary<string, string>();
        for (int i = 0; i < TabNames.Length; i++)
        {
            var range = valueRanges[i] as JObject;
            string returnedRange = (string)range?["range"];
            string expected = "'" + TabNames[i] + "'!";
            if (returnedRange == null || !(returnedRange.StartsWith(expected, StringComparison.Ordinal) || returnedRange.StartsWith(TabNames[i] + "!", StringComparison.Ordinal)))
                throw new FormatException("다른 시트 범위가 반환되었습니다: " + TabNames[i]);
            var rows = range["values"] as JArray;
            if (rows == null || rows.Count == 0) throw new FormatException(TabNames[i] + ": 헤더가 없습니다.");
            var header = (JArray)rows[0];
            int idIndex = header.Select((cell, index) => new { cell, index }).Where(x => x.cell.ToString().Trim().TrimStart('\uFEFF') == "id").Select(x => x.index).DefaultIfEmpty(-1).First();
            if (idIndex < 0) throw new FormatException(TabNames[i] + ": id 열이 없습니다.");
            var output = new StringBuilder();
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex] as JArray;
                if (row == null) throw new FormatException(TabNames[i] + ": 행 형식 오류");
                if (rowIndex > 0 && (row.Count <= idIndex || string.IsNullOrWhiteSpace(row[idIndex].ToString()))) continue;
                output.AppendLine(string.Join(",", Enumerable.Range(0, header.Count).Select(column => Quote(column < row.Count ? row[column].ToString() : ""))));
            }
            files.Add(CsvNames[i], output.ToString());
        }
        return files;
    }

    private static async Task<string> GetAccessTokenAsync(CancellationToken cancellation)
    {
        if (!HasToken) throw new InvalidOperationException("먼저 'Google 로그인'을 진행해주세요.");
        var token = JsonUtility.FromJson<TokenState>(ReadProtected("token.dat"));
        if (token == null || string.IsNullOrEmpty(token.refreshToken)) throw new InvalidOperationException("저장된 인증이 유효하지 않습니다. 다시 로그인해주세요.");
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!string.IsNullOrEmpty(token.accessToken) && token.expiresAt > now + 60) return token.accessToken;
        var settings = ReadClient();
        token = await ExchangeAsync(new Dictionary<string, string>
        {
            ["client_id"] = settings.clientId, ["client_secret"] = settings.clientSecret,
            ["refresh_token"] = token.refreshToken, ["grant_type"] = "refresh_token"
        }, token.refreshToken, cancellation);
        SaveProtected("token.dat", JsonUtility.ToJson(token));
        return token.accessToken;
    }

    private static async Task<TokenState> ExchangeAsync(Dictionary<string, string> fields, string previousRefreshToken, CancellationToken cancellation)
    {
        using var response = await Http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(fields), cancellation);
        string body = await response.Content.ReadAsStringAsync(); cancellation.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode)
        {
            // Do not log response bodies or token requests: they can contain credentials.
            string code; try { code = (string)JObject.Parse(body)["error"]; } catch { code = "unknown"; }
            if (code == "invalid_grant") throw new InvalidOperationException("Google 인증이 만료되었거나 해제되었습니다. 다시 로그인해주세요.");
            throw new InvalidOperationException("Google 토큰 발급 실패 (HTTP " + (int)response.StatusCode + "). OAuth 설정을 확인해주세요.");
        }
        var parsed = JObject.Parse(body);
        string access = (string)parsed["access_token"];
        int expires = (int?)parsed["expires_in"] ?? 0;
        if (string.IsNullOrWhiteSpace(access) || expires < 1) throw new FormatException("Google 토큰 응답이 올바르지 않습니다.");
        return new TokenState { accessToken = access, refreshToken = (string)parsed["refresh_token"] ?? previousRefreshToken, expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expires };
    }

    private static async Task<JObject> GetJsonAsync(string url, string token, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await Http.SendAsync(request, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new InvalidOperationException("Google 인증을 다시 진행해주세요.");
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new InvalidOperationException("시트 읽기 권한 또는 Google Sheets API 활성화 상태를 확인해주세요. 다른 계정이면 다시 로그인해주세요.");
            if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("시트 ID 또는 해당 계정의 파일 접근 권한을 확인해주세요.");
            throw new InvalidOperationException("Google Sheets 조회 실패 (HTTP " + (int)response.StatusCode + "). 잠시 후 다시 시도해주세요.");
        }
        string body = await response.Content.ReadAsStringAsync(); cancellation.ThrowIfCancellationRequested();
        return JObject.Parse(body);
    }

    private static ClientSettings ReadClient()
    {
        if (!HasClient) throw new InvalidOperationException("먼저 데스크톱 OAuth 클라이언트 JSON을 가져와주세요.");
        return JsonUtility.FromJson<ClientSettings>(ReadProtected("client.dat"));
    }
    private static void SaveProtected(string file, string json)
    {
#if UNITY_EDITOR_WIN
        Directory.CreateDirectory(LocalSettingsFolder);
        byte[] encrypted = ProtectBytes(Encoding.UTF8.GetBytes(json), true);
        string path = Path.Combine(LocalSettingsFolder, file), temp = path + ".tmp";
        File.WriteAllBytes(temp, encrypted);
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
#else
        throw new PlatformNotSupportedException("현재 인증 저장은 Windows 에디터를 지원합니다.");
#endif
    }
    private static string ReadProtected(string file)
    {
#if UNITY_EDITOR_WIN
        return Encoding.UTF8.GetString(ProtectBytes(File.ReadAllBytes(Path.Combine(LocalSettingsFolder, file)), false));
#else
        throw new PlatformNotSupportedException("현재 인증 저장은 Windows 에디터를 지원합니다.");
#endif
    }
    private static byte[] ProtectBytes(byte[] bytes, bool encrypt)
    {
        var type = Type.GetType("System.Security.Cryptography.ProtectedData, System.Security", true);
        var scope = Type.GetType("System.Security.Cryptography.DataProtectionScope, System.Security", true);
        var method = type.GetMethod(encrypt ? "Protect" : "Unprotect", new[] { typeof(byte[]), typeof(byte[]), scope });
        return (byte[])method.Invoke(null, new object[] { bytes, null, Enum.Parse(scope, "CurrentUser") });
    }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string ColumnName(int number) { string s = ""; while (number > 0) { number--; s = (char)('A' + number % 26) + s; number /= 26; } return s; }
    private static string Encode(Dictionary<string, string> values) => string.Join("&", values.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string RandomUrlToken(int count) { var bytes = new byte[count]; using var random = RandomNumberGenerator.Create(); random.GetBytes(bytes); return Base64Url(bytes); }
    private static void Reply(HttpListenerContext context, int status, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("<!doctype html><meta charset='utf-8'><p>" + WebUtility.HtmlEncode(message) + "</p>");
        context.Response.StatusCode = status; context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length; context.Response.OutputStream.Write(bytes, 0, bytes.Length); context.Response.Close();
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Inspection.Contracts;

namespace Inspection.Hmi.Services;

/// <summary>
/// 지금 로그인한 작업자와, 오프라인 로그인용 저장소.
///
/// 오프라인 로그인: MES에 한 번 로그인에 성공한 작업자는 사번과 PIN의 해시를 이 PC에 저장해 둔다.
/// MES가 꺼져 있으면 저장된 해시와 비교해 로그인시킨다 (README 원칙: MES가 끊겨도 검사는 계속된다).
/// PIN 자체는 저장하지 않는다. PBKDF2(SHA-256, 10만 회) 해시 + 작업자별 무작위 salt만 저장한다.
/// </summary>
public sealed class WorkerSession
{
    private record CachedWorker(string EmployeeId, string Name, string Role, string Salt, string Hash);

    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PcbAoiHmi", "workers.json");

    /// <summary>지금 로그인한 작업자. 없으면 null.</summary>
    public LoginResult? Current { get; private set; }

    public void SignIn(LoginResult worker) => Current = worker;
    public void SignOut() => Current = null;

    /// <summary>MES 로그인에 성공했을 때 호출. 다음에 MES가 꺼져 있어도 이 작업자는 로그인할 수 있다.</summary>
    public void Remember(string employeeId, string pin, LoginResult worker)
    {
        try
        {
            var cache = Load();
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            cache[employeeId] = new CachedWorker(worker.EmployeeId, worker.Name, worker.Role,
                Convert.ToBase64String(salt), Convert.ToBase64String(Hash(pin, salt)));
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(cache));
        }
        catch
        {
            // 저장 실패는 온라인 로그인에 영향을 주지 않는다
        }
    }

    /// <summary>MES에 연결할 수 없을 때 저장된 정보로 로그인을 시도한다. 틀리거나 기록이 없으면 null.</summary>
    public LoginResult? TryOffline(string employeeId, string pin)
    {
        if (!Load().TryGetValue(employeeId, out var w)) return null;
        byte[] expected = Convert.FromBase64String(w.Hash);
        byte[] actual = Hash(pin, Convert.FromBase64String(w.Salt));
        // 비교 시간이 일치 길이에 따라 달라지지 않게 (시간차 공격 방지)
        return CryptographicOperations.FixedTimeEquals(expected, actual)
            ? new LoginResult(w.EmployeeId, w.Name, w.Role, Offline: true)
            : null;
    }

    private static byte[] Hash(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, 100_000, HashAlgorithmName.SHA256, 32);

    private static Dictionary<string, CachedWorker> Load()
    {
        try
        {
            return File.Exists(CacheFile)
                ? JsonSerializer.Deserialize<Dictionary<string, CachedWorker>>(File.ReadAllText(CacheFile)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }
}

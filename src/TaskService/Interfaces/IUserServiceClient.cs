namespace TaskService.Interfaces;

/// <summary>
/// UserService'e senkron HTTP çağrısı yapan istemci. TaskService, görev bir kullanıcıya
/// atanmadan önce o kullanıcının var olduğunu bu istemci üzerinden doğrular.
/// </summary>
public interface IUserServiceClient
{
    /// <summary>Kullanıcı varsa <c>true</c>, yoksa <c>false</c> döner.</summary>
    /// <exception cref="UserServiceUnavailableException">UserService'e ulaşılamadığında (retry'lar tükendiğinde, timeout veya açık circuit breaker).</exception>
    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken);
}

public sealed class UserServiceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

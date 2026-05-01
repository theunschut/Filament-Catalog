public interface IOwnerService
{
    Task<List<Owner>> GetAllAsync();
    Task<Owner> CreateAsync(string name);
    Task DeleteAsync(int id);
}

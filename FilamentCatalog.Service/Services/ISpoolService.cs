public interface ISpoolService
{
    Task<List<Spool>> GetAllAsync();
    Task<Spool> CreateAsync(SpoolCreateRequest req);
    Task<Spool> UpdateAsync(int id, SpoolUpdateRequest req);
    Task DeleteAsync(int id);
}

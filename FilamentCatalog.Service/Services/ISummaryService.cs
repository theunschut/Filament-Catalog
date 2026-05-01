public interface ISummaryService
{
    Task<SummaryDto> GetSummaryAsync();
    Task<IEnumerable<BalanceRowDto>> GetBalanceAsync();
}

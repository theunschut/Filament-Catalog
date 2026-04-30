public class Owner
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsMe { get; set; }
    public DateTime CreatedAt { get; set; }
}

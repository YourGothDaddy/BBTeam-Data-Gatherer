namespace BBTeam_Data_Gatherer
{
    using BBTeam_Data_Gatherer.Models;

    public class Program
    {
        static async Task Main(string[] args)
        {
            var gatherer = new Gatherer();

            await gatherer.Gather();
        }
    }
}

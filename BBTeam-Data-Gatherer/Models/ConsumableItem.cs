namespace BBTeam_Data_Gatherer.Models
{
    public class ConsumableItem
    {
        public int Id { get; set; }

        public string MainCategory { get; set; }

        public string Title { get; set; }

        public string SubTitle { get; set; }

        public float? CaloriesPer100g { get; set; }

        public float? ProteinPer100g { get; set; }

        public float? CarbohydratesPer100g { get; set; }

        public float? FatsPer100g { get; set; }

        public Carbohydrates Carbohydrates { get; set; }

        public AminoAcids AminoAcids { get; set; }

        public Fats Fats { get; set; }

        public Minerals Minerals { get; set; }

        public Other Other { get; set; }

        public Sterols Sterols { get; set; }

        public Vitamins Vitamins { get; set; }
    }
}

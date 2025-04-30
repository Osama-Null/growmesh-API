namespace growmesh_API.DTOs.ResponseDTOs
{
    public class SavingsGoalTrendDTO
    {
        public DateTime PeriodEnd { get; set; }
        public decimal CumulativeSavings { get; set; }
        public decimal Difference { get; set; }
        public decimal? TargetCumulativeSavings { get; set; }
    }
}

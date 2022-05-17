namespace QLab.Robot.Bankrupt.Efrsb.MessageService.Services
{
    public class CreateTaskResult
    {
        public CreateTaskResult(bool isFailed = false)
        {
            IsFailed = isFailed;
        }
        public bool IsSucceeded => InnerResults!.All(x => !x.IsFailed);
        public bool IsFailed { get; }
        public string? Message { get; set; }
        public List<CreateTaskResult>? InnerResults { get; set; } = new List<CreateTaskResult>();
    }
}

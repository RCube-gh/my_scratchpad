using System;

namespace WannaDoWidget
{
    public enum WannaDoState
    {
        Todo,
        Completed,
        Aborted,
        Expired
    }

    public class WannaDoItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Memo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? DueDate { get; set; }
        public WannaDoState State { get; set; } = WannaDoState.Todo;

        public bool CheckExpiration()
        {
            if (State == WannaDoState.Todo && DueDate.HasValue && DueDate.Value.Date < DateTime.Today)
            {
                State = WannaDoState.Expired;
                return true;
            }
            return false;
        }
    }
}

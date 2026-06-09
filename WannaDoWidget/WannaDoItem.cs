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
        public bool DueTimeSpecified { get; set; }
        public WannaDoState State { get; set; } = WannaDoState.Todo;

        public bool CheckExpiration()
        {
            if (State != WannaDoState.Todo || !DueDate.HasValue)
            {
                return false;
            }

            DateTime expiresAt = DueTimeSpecified
                ? DueDate.Value
                : DueDate.Value.Date.AddDays(1);

            if (expiresAt <= DateTime.Now)
            {
                State = WannaDoState.Expired;
                return true;
            }
            return false;
        }
    }
}

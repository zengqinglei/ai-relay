namespace Leistd.Exception.Core
{
    public abstract class BusinessException : CommonException
    {
        protected string CodePrefix { get; private set; }

        public int Code { get; private set; }

        public string? Details { get; private set; }

        protected BusinessException(string codePrefix, string message, System.Exception? innerException = null)
            : base(message, innerException)
        {
            this.CodePrefix = codePrefix;
            this.Code = int.Parse(codePrefix + "00");
        }

        public BusinessException WithCode(string code)
        {
            this.Code = int.Parse(CodePrefix + code);
            return this;
        }

        public BusinessException WithDetails(string details)
        {
            this.Details = details;
            return this;
        }

        public string? GetStackTraceStr()
        {
            return base.StackTrace;
        }

        public override string ToString()
        {
            return this.GetType().Name + " : [code=" + Code + ", message=" + Message + ", details=" + Details + "]";
        }
    }
}

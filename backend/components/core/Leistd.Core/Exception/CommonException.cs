namespace Leistd.Exception;

public class CommonException(string message, System.Exception? innerException = null) : System.Exception(message, innerException);


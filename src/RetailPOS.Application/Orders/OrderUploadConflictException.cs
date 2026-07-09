namespace RetailPOS.Application.Orders;

public sealed class OrderUploadConflictException(string message) : Exception(message);

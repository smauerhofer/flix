namespace Flix.Core;

public static class ScreeningSideResolver
{
  public static string Resolve(string partyId)
  {
    return partyId.Trim().ToUpperInvariant() switch
    {
      "SENDER" or "DEBTOR" or "ORIGINATOR" or "PAYER" => "SENDER",
      "RECEIVER" or "CREDITOR" or "BENEFICIARY" or "PAYEE" => "RECEIVER",
      _ => throw new ArgumentException($"Unsupported party side: {partyId}.")
    };
  }
}

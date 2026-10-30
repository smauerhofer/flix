namespace Flix.Core;

public sealed class ScreeningService(
  FilterManager manager,
  ScreeningStrategyRegistry strategies)
{
  public ScreeningResponse Screen(ScreeningRequest request)
  {
    ArgumentNullException.ThrowIfNull(request);
    using var lease = manager.Acquire();
    var validationFindings = new List<ScreeningValidationFinding>();
    var names = new List<NameMatch>();
    var fields = new List<FieldMatch>();
    foreach (var field in request.Fields)
    {
      var validation = ScreeningFieldValidator.Validate(
        field, ScreeningSideResolver.Resolve(field.PartyId));
      if (validation is not null)
      {
        validationFindings.Add(validation);
        continue;
      }
      var matches = strategies.Match(lease.Snapshot.Normal, field);
      names.AddRange(matches.Names);
      fields.AddRange(matches.Fields);
    }
    // Country/address hits are evidence only. Legal restriction checks and
    // entity/party correlation are not implemented by this starter application.
    string status = validationFindings.Count > 0 ? "DATA_QUALITY_REVIEW" :
      names.Count + fields.Count > 0 ? "POTENTIAL_MATCHES" : "NO_CANDIDATES";
    return new ScreeningResponse(request.MessageId, lease.Snapshot.Version,
      status, names, fields, validationFindings);
  }
}

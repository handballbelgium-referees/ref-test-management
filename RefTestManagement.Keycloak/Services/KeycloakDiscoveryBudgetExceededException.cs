namespace Handball.Belgium.RefTestManagement.Keycloak;

/// <summary>
/// Thrown when approver discovery needs more Keycloak admin requests than its budget allows.
/// Callers must treat this as a failed lookup, never as a partial recipient list.
/// </summary>
public sealed class KeycloakDiscoveryBudgetExceededException(string message) : InvalidOperationException(message);

# MaintenancePRO API — ASP.NET Core / C# / .NET 10

Backend indépendant du client .NET MAUI Windows/Android. Données persistées dans SQLite (`maintenancepro.db`), fichiers photo dans `uploads/photos/`. L'API est une base complète à intégrer et à valider avec les règles exactes de ton organisation; ce n'est pas une certification OWASP.

## 1. Prérequis et lancement sous Windows

Installe le SDK .NET 10. Ouvre PowerShell dans `MaintenancePRO.Api` :

```powershell
dotnet user-secrets init
$keyBytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($keyBytes)
$key = [Convert]::ToBase64String($keyBytes)
$rng.Dispose()
dotnet user-secrets set "Jwt:Key" $key
dotnet user-secrets set "BootstrapAdmin:Email" "admin@maintenancepro.local"
$pwd = Read-Host "Mot de passe administrateur fort (12+ caractères, majuscule, minuscule, chiffre et symbole)" -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwd)
try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr); dotnet user-secrets set "BootstrapAdmin:Password" $plain }
finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
dotnet restore
dotnet run --urls "http://0.0.0.0:5080"
```

Swagger en développement : `http://localhost:5080/swagger`. Le premier démarrage crée la base, les rôles et l'administrateur initial si les deux secrets bootstrap sont présents. Le compte d'inscription ordinaire est `Pending`; l'administrateur doit l'activer.

## 2. Utiliser le JWT dans Swagger (point important)

1. Appelle `POST /api/auth/login` avec l'e-mail et le mot de passe de l'administrateur.
2. Copie la valeur `accessToken` de la réponse JSON (pas les guillemets).
3. Clique **Authorize** en haut de Swagger et colle uniquement le jeton. Swagger ajoute le préfixe `Bearer` automatiquement.
4. Appelle un endpoint protégé. Si tu obtiens `401`, vérifie la date d'expiration (15 min), l'issuer/audience et que la clé JWT est identique depuis le lancement. Si `403`, le jeton est accepté mais le rôle ou la permission est insuffisant.

La définition OpenAPI inclut explicitement le schéma HTTP Bearer JWT, donc l'interface Swagger peut transmettre le jeton.

## 3. Rôles

- `Admin`: gestion utilisateurs/rôles/statuts, audit, toutes les fonctions.
- `Supervisor`: gestion équipements, décisions de diagnostic, attribution et pilotage des interventions.
- `Technician`: diagnostics, interventions attribuées et photos autorisées.
- `User`: déclaration de panne, lecture limitée à ses déclarations et interventions créées par lui.

Les contrôles sont côté serveur; cacher un bouton côté MAUI ne constitue pas une permission.

## 4. Endpoints

| Groupe | Routes principales |
|---|---|
| Authentification | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Équipements | `GET/POST /api/equipment`, `GET/PUT/DELETE /api/equipment/{id}`, `GET /api/equipment/{id}/history` |
| Pannes | `GET/POST /api/breakdowns`, `GET /api/breakdowns/{id}`, `PATCH /api/breakdowns/{id}/status` |
| Diagnostics | `GET/POST /api/diagnoses`, `GET /api/diagnoses/{id}`, `PATCH /api/diagnoses/{id}/decision` |
| Interventions | `GET/POST /api/interventions`, `GET/PATCH /api/interventions/{id}` |
| Photos | `POST /api/photos/breakdown/{breakdownId}`, `POST /api/photos/intervention/{interventionId}`, `GET /api/photos/{id}`, `GET /api/photos/breakdown/{breakdownId}` |
| Dashboard/rapports | `GET /api/dashboard`, `GET /api/dashboard/reports?fromUtc=...&toUtc=...` |
| Administration | `GET /api/admin/users`, `PATCH /api/admin/users/{id}/status`, `PUT /api/admin/users/{id}/role`, `GET /api/admin/audit`, `GET /api/admin/dashboard` |

## 5. États métier

- Équipement : `Available`, `InService`, `ToInspect`, `Broken`, `Retired`.
- Panne : `Reported`, `Diagnosing`, `AwaitingApproval`, `InMaintenance`, `Resolved`, `Rejected`, `Closed`.
- Diagnostic : `Draft`, `Submitted`, `Approved`, `Rejected`.
- Intervention : `Planned`, `Assigned`, `InProgress`, `Paused`, `Completed`, `Cancelled`, `Rejected`.
- Sévérité : `Low`, `Medium`, `High`, `Critical`.

## 6. Sécurité incluse

ASP.NET Core Identity et hachage de mots de passe, politique de mot de passe, verrouillage après échecs, JWT signé et expiration courte, validation issuer/audience/signature/lifetime, autorisations par rôle, contrôle d'accès aux ressources, validation des DTO, limitation de débit sur l'auth, pagination plafonnée, journal d'audit, en-têtes HTTP, taille/type des photos limités et noms de fichiers générés côté serveur.

## 7. Avant toute mise en production

- HTTPS obligatoire; ne pas exposer le serveur de développement HTTP sur Internet.
- Remplacer `EnsureCreated` par des migrations EF Core pour les mises à jour du schéma.
- Ajouter tests automatisés unitaires/intégration, tests OWASP API Top 10 et revue des règles métier.
- Prévoir récupération/réinitialisation du mot de passe, vérification e-mail, révocation/renouvellement de jetons et journalisation centralisée.
- Stocker les photos dans un stockage privé durable avec antivirus/scanning et sauvegardes.
- Pour plusieurs instances ou une charge importante, choisir PostgreSQL/SQL Server et des sauvegardes testées.
- Configurer CORS uniquement pour les origines réellement nécessaires si un client navigateur est ajouté. MAUI natif n'exige pas CORS.
- SQLite et fichiers locaux persistent sur la machine serveur; ils ne synchronisent pas automatiquement des appareils entre eux.

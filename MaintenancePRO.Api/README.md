# MaintenancePRO — application MAUI + backend ASP.NET Core (.NET 10)

Ce dossier contient le front-end MAUI Windows/Android et le backend indépendant `MaintenancePRO.Api`.

## Lancer le backend
1. Installer le SDK .NET 10 et ouvrir un terminal dans `MaintenancePRO.Api`.
2. Définir les secrets de développement (ne jamais les committer) :
```powershell
dotnet user-secrets init
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Jwt:Key" $key
dotnet user-secrets set "BootstrapAdmin:Email" "admin@maintenancepro.local"
$pwd = Read-Host "Mot de passe admin fort (12+ caractères)" -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($pwd)
try { $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr); dotnet user-secrets set "BootstrapAdmin:Password" $plain } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
```
3. `dotnet restore`, puis `dotnet run`. En Development, Swagger est disponible à `http://localhost:5080/swagger`.
4. Le compte bootstrap est créé uniquement si les deux paramètres BootstrapAdmin sont configurés et si l'email n'existe pas déjà.

## Lancer MAUI
Ouvrir `MaintenancePRO_Multiplateforme/MaintenancePRO.csproj` dans Visual Studio avec les workloads MAUI .NET 10. L'authentification de l'écran Login utilise maintenant l'API et stocke le jeton dans `SecureStorage`.

`Services/ApiBaseUrl.cs` : Windows utilise `http://localhost:5080/`, Android Emulator utilise `http://10.0.2.2:5080/`. Pour un vrai téléphone, remplace `10.0.2.2` par l'IP LAN du PC, connecte les appareils au même réseau et autorise le port 5080 dans le pare-feu. HTTP/cleartext est réservé au développement local. En production, configure HTTPS et un certificat valide; retire `android:usesCleartextTraffic="true"`.

## Endpoints
- `POST /api/auth/register` : crée un compte Pending, non connectable avant activation
- `POST /api/auth/login`, `GET /api/auth/me`
- `GET/POST/PUT/DELETE /api/equipment` (contrôle de rôle selon action)
- `GET/POST /api/interventions`, `GET /api/interventions/{id}`, `PATCH /api/interventions/{id}`
- `GET /api/admin/users`, `PATCH /api/admin/users/{id}/status`, `PUT /api/admin/users/{id}/role`
- `GET /api/admin/audit`, `GET /api/admin/dashboard`

## Rôles et statuts
Rôles : Admin, Supervisor, Technician, User. Statuts de compte : Pending, Active, Suspended, Disabled. Statuts d'intervention : Submitted, Approved, Assigned, InProgress, Completed, Rejected, Cancelled, Closed. Les statuts et les rôles sont validés côté serveur.

## Sécurité et limites
Le backend applique Identity/password hashing, verrouillage après échecs, JWT court (15 min) avec validation issuer/audience/signature/expiration, limitation de débit auth, politiques de rôle, contrôle d'accès par propriétaire/technicien, audit des actions sensibles, en-têtes de sécurité, validation des DTO et requêtes EF Core paramétrées.

C'est une base de développement, pas une certification OWASP. Avant production : utiliser HTTPS uniquement, migrations EF Core au lieu de EnsureCreated, sauvegardes, secrets gérés, tests de sécurité et d'intégration, CORS restrictif si nécessaire, reset mot de passe/vérification e-mail, stratégie de révocation/renouvellement de jetons et revue de toutes les règles métier. SQLite convient à la démonstration; évaluer SQL Server/PostgreSQL pour un déploiement multi-utilisateur.

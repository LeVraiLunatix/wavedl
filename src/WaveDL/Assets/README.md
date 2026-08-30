# Assets

WaveDL n'embarque aucune ressource binaire obligatoire — l'interface utilise les
*Segoe Fluent Icons* du système.

Optionnel : déposez ici `wavedl.ico` pour donner une icône à l'exécutable, puis ajoutez
dans `WaveDL.csproj` :

```xml
<PropertyGroup>
  <ApplicationIcon>Assets\wavedl.ico</ApplicationIcon>
</PropertyGroup>
```

Les fichiers de ce dossier sont copiés à côté de l'exécutable au build.

-- Le tri par défaut de la liste des clients porte sur (Nom, Prenom) :
-- un index sur ces deux colonnes évite un tri à chaque page.
--
-- IX_Client_Nom est conservé volontairement : plus petit, c'est lui que
-- SQLite parcourt pour compter les lignes. Mesuré sur 100 000 clients,
-- le supprimer fait passer le comptage de 1 ms à 9 ms à chaque page.
CREATE INDEX IF NOT EXISTS IX_Client_Nom_Prenom
ON Client(Nom, Prenom);

using LumDbEngine.Element.Engine.Cache;
using LumDbEngine.Element.Exceptions;
using LumDbEngine.Element.Manager.Common;
using LumDbEngine.Element.Structure.Page;
using LumDbEngine.Element.Structure.Page.Key;
using LumDbEngine.Element.Structure.Page.KeyIndex;
using LumDbEngine.Element.Structure.Page.Repo;

namespace LumDbEngine.Element.Manager.Specific
{
    internal static class KeyManager
    {
        internal static RepoNode InsertKey(DbCache db, TablePage tablePage, in IndexNode indexNode, in RepoNodeKey key)
        {
            return SearchAndInsertKey(db, tablePage, indexNode, key);
        }

        internal static RepoNode? SearchKey(DbCache db, TablePage tablePage, in IndexNode? indexNode, in RepoNodeKey key)
        {
            if (indexNode != null)
            {
                return SearchKey(db, tablePage, indexNode.Value.KeyLink, key);
            }
            else
            {
                return null;
            }
        }

        private static RepoNode SearchAndInsertKey(DbCache db, TablePage tablePage, in IndexNode indexNode, in RepoNodeKey key)
        {
            NodeLink link = indexNode.KeyLink;
            RepoNode? lastNode = null;

            while (true)
            {
                var keyNode = NodeManager.GetRepoNode(db, link);
                if (keyNode != null)
                {
                    // Hash Collision
                    LumException.ThrowIfTrue(keyNode.Value.IsKeyEqual(key), "Key already existed");
                    lastNode = keyNode;
                    link = keyNode.Value.NextKeyNodeLink;
                }
                else
                {
                    var newKeyNode = KeyRepoManager.RequestAvailableRepoNode(db, tablePage);
                    newKeyNode.SetKey(key);

                    if (lastNode != null)    // link the last node and new created node
                    {
                        var newLastNode = lastNode.Value;
                        NodeManager.SetLink(db, ref newLastNode.NextKeyNodeLink, newKeyNode);
                        newLastNode.Update(db);

                        NodeManager.SetLink(db, ref newKeyNode.PrevKeyNodeLink, lastNode.Value);
                        newKeyNode.Update(db);
                    }
                    else
                    {
                        var tmpNode = indexNode;
                        NodeManager.SetLink(db, ref tmpNode.KeyLink, newKeyNode);
                        tmpNode.Update(db);
                        newKeyNode.Update(db);
                    }
                    return newKeyNode;
                }
            }
        }

        private static RepoNode? SearchKey(DbCache db, TablePage tablePage, NodeLink link, in RepoNodeKey key)
        {
            while (true)
            {
                var keyNode = NodeManager.GetRepoNode(db, link);

                if (keyNode != null)
                {
                    if (keyNode.Value.IsKeyEqual(key))
                    {
                        return keyNode;
                    }
                    else
                    {
                        link = keyNode.Value.NextKeyNodeLink;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        internal static void GetRepoPages(DbCache db, in NodeLink nodeLink, HashSet<uint> pages)
        {
            var keyNode = NodeManager.GetRepoNode(db, nodeLink);

            while (keyNode != null)
            {
                pages.Add(keyNode.Value.HostPageId);

                if (db.IsValidPage(keyNode.Value.NextKeyNodeLink.TargetPageID))
                {
                    keyNode = NodeManager.GetRepoNode(db, keyNode.Value.NextKeyNodeLink);
                }
                else
                {
                    break;
                }
            }
        }
    }
}
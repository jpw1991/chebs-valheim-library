namespace ChebsValheimLibrary.Minions
{
    /// <summary>
    /// A component that can be added to a rock in order to destroy it after a period.
    /// </summary>
    public class NukeRock : NukeTree
    {
        protected override void Nuke()
        {
            var destructible = GetComponentInParent<Destructible>();
            if (destructible != null)
            {
                var hitData = new HitData();
                hitData.m_damage.m_pickaxe = 500;
                hitData.m_toolTier = 100;
                destructible.Damage(hitData);
                return;
            }

            var mineRock5 = GetComponentInParent<MineRock5>();
            if (mineRock5 != null)
            {
                // destroy all fragments
                for (int i = 0; i < mineRock5.m_hitAreas.Count; i++)
                {
                    var hitArea = mineRock5.m_hitAreas[i];
                    if (hitArea.m_health > 0f)
                    {
                        var hitData = new HitData();
                        hitData.m_damage.m_damage = hitArea.m_health;
                        hitData.m_point = hitArea.m_collider.bounds.center;
                        hitData.m_toolTier = 100;
                        mineRock5.DamageArea(i, hitData);
                    }
                }

                return;
            }

            var mineRock = GetComponentInParent<MineRock>();
            if (mineRock != null)
            {
                // destroy all fragments
                for (int i = 0; i < mineRock.m_hitAreas.Length; i++)
                {
                    var col = mineRock.m_hitAreas[i];
                    if (col.TryGetComponent(out HitArea hitArea) && hitArea.m_health > 0f)
                    {
                        var hitData = new HitData();
                        hitData.m_damage.m_damage = hitArea.m_health;
                        hitData.m_point = col.bounds.center;
                        hitData.m_toolTier = 100;
                        mineRock5.DamageArea(i, hitData);
                    }
                }
            }
        }
    }
}
﻿using UnityEngine;

namespace ChebsValheimLibrary.Minions
{
    /// <summary>
    /// The FreshMinion is added and used as a way of determining whether a minion was freshly created or whether it
    /// is pre-existing. As such, different functionality can be implemented in the minion's Awake or other methods.
    /// </summary>
    public class FreshMinion : MonoBehaviour
    {
        // Attention: ONLY add FreshMinion to a freshly created by a wand.
        // Under no other circumstances should it be added to a minion.
        // This is to get around a very annoying problem in the
        // merge request below where if default follow is enabled minions
        // will not attack.
        //
        // FreshMinon must ALWAYS be added BEFORE the UndeadMinion component.
        // but ONLY if creating freshly from a wand.
        //
        // https://github.com/jpw1991/chebs-necromancy/pull/68
    }
}

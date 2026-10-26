using System;
using UnityEngine;

namespace Study3D.FpsGame.Weapon
{
    public partial class Weapon
    {
        [Header("Settings")] 
        [SerializeField] private string weaponName;
        [SerializeField] private float fireRate = 0.05f;
        [SerializeField] private int damage = 10;
        [SerializeField] private float range = 100.0f; // 유효 공격 범위거리

        public string WeaponName => weaponName;
        public float FireRate => fireRate;
        public int Damage => damage;
        public float Range => range;
    }
}
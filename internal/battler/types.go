package battler

import "math"

// UnitType defines the stats for a class of unit.
type UnitType struct {
	Name            string  `json:"name"`
	DisplayName     string  `json:"display_name"`
	Health          int     `json:"health"`
	Speed           float64 `json:"speed"`
	AttackDamage    int     `json:"attack_damage"`
	AttackRange     float64 `json:"attack_range"`
	AttackCooldown  float64 `json:"attack_cooldown"`  // seconds between attacks
	ProjectileSpeed float64 `json:"projectile_speed"` // 0 = melee
	SplashRadius    float64 `json:"splash_radius"`    // 0 = single target
	Cost            int     `json:"cost"`
	Description     string  `json:"description"`
}

// DefaultUnitTypes returns the 6 unit types matching the Unity client catalog.
func DefaultUnitTypes() []UnitType {
	return []UnitType{
		{
			Name: "crawler", DisplayName: "Crawler",
			Health: 50, Speed: 3.0,
			AttackDamage: 10, AttackRange: 1.5, AttackCooldown: 0.8,
			ProjectileSpeed: 0, SplashRadius: 0, Cost: 100,
			Description: "Fast melee unit. Cheap and numerous.",
		},
		{
			Name: "archer", DisplayName: "Archer",
			Health: 30, Speed: 2.0,
			AttackDamage: 15, AttackRange: 8.0, AttackCooldown: 1.5,
			ProjectileSpeed: 15.0, SplashRadius: 0, Cost: 150,
			Description: "Ranged unit. Fragile but long reach.",
		},
		{
			Name: "tank", DisplayName: "Tank",
			Health: 200, Speed: 1.0,
			AttackDamage: 40, AttackRange: 5.0, AttackCooldown: 2.0,
			ProjectileSpeed: 20.0, SplashRadius: 0, Cost: 300,
			Description: "Heavy armored unit. Slow but durable.",
		},
		{
			Name: "artillery", DisplayName: "Artillery",
			Health: 60, Speed: 0.5,
			AttackDamage: 80, AttackRange: 15.0, AttackCooldown: 4.0,
			ProjectileSpeed: 10.0, SplashRadius: 3.0, Cost: 400,
			Description: "Long-range splash damage. Very slow.",
		},
		{
			Name: "scout", DisplayName: "Scout",
			Health: 20, Speed: 5.0,
			AttackDamage: 5, AttackRange: 2.0, AttackCooldown: 0.5,
			ProjectileSpeed: 0, SplashRadius: 0, Cost: 50,
			Description: "Extremely fast but weak. Good for flanking.",
		},
		{
			Name: "healer", DisplayName: "Healer",
			Health: 40, Speed: 2.0,
			AttackDamage: -10, AttackRange: 6.0, AttackCooldown: 2.0,
			ProjectileSpeed: 0, SplashRadius: 2.0, Cost: 250,
			Description: "Heals nearby friendly units. Negative damage = healing.",
		},
	}
}

// GetUnitType looks up a unit type by name.
func GetUnitType(name string) *UnitType {
	for _, ut := range DefaultUnitTypes() {
		if ut.Name == name {
			return &ut
		}
	}
	return nil
}

// Unit is a live unit in a battle simulation.
type Unit struct {
	ID             string  `json:"id"`
	TypeName       string  `json:"type_name"`
	OwnerID        int     `json:"owner_id"`
	X              float64 `json:"x"`
	Y              float64 `json:"y"`
	Health         int     `json:"health"`
	MaxHealth      int     `json:"max_health"`
	TargetID       string  `json:"target_id"`
	Alive          bool    `json:"alive"`
	AttackCooldown float64 `json:"-"` // remaining cooldown in seconds
}

// Projectile is a live projectile in the simulation.
type Projectile struct {
	ID      string  `json:"id"`
	OwnerID int     `json:"owner_id"`
	X       float64 `json:"x"`
	Y       float64 `json:"y"`
	TargetX float64 `json:"target_x"`
	TargetY float64 `json:"target_y"`
	Speed   float64 `json:"speed"`
	Damage  int     `json:"damage"`
	Splash  float64 `json:"splash"`
}

// ArmyPlacement is a request to place a unit at a specific position.
type ArmyPlacement struct {
	TypeName string  `json:"type_name"`
	X        float64 `json:"x"`
	Y        float64 `json:"y"`
}

// BattleState is the complete state broadcast to clients each tick.
type BattleState struct {
	Tick        int           `json:"tick"`
	Status      string        `json:"status"` // "running" or "finished"
	Units       []Unit        `json:"units"`
	Projectiles []Projectile  `json:"projectiles"`
	WinnerID    int           `json:"winner_id"`
}

// Distance returns the Euclidean distance between two points.
func Distance(x1, y1, x2, y2 float64) float64 {
	dx := x2 - x1
	dy := y2 - y1
	return math.Sqrt(dx*dx + dy*dy)
}

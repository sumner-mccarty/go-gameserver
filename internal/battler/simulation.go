package battler

import (
	"fmt"
	"math"
)

const (
	// TickRate is the number of simulation ticks per second.
	TickRate = 20
	// DeltaTime is the time step per tick in seconds.
	DeltaTime = 1.0 / float64(TickRate)
)

// Simulation holds the complete mutable state of a running battle.
type Simulation struct {
	Tick        int
	Status      string // "running" or "finished"
	Units       []*Unit
	Projectiles []*Projectile
	WinnerID    int
	nextProjID  int
}

// NewSimulation creates a simulation from two armies.
func NewSimulation(player1ID, player2ID int, army1, army2 []ArmyPlacement) (*Simulation, error) {
	sim := &Simulation{
		Tick:   0,
		Status: "running",
	}

	// Spawn army 1
	for i, placement := range army1 {
		ut := GetUnitType(placement.TypeName)
		if ut == nil {
			return nil, fmt.Errorf("unknown unit type: %s", placement.TypeName)
		}
		sim.Units = append(sim.Units, &Unit{
			ID:        fmt.Sprintf("p%d_u%d", player1ID, i),
			TypeName:  ut.Name,
			OwnerID:   player1ID,
			X:         placement.X,
			Y:         placement.Y,
			Health:    ut.Health,
			MaxHealth: ut.Health,
			Alive:     true,
		})
	}

	// Spawn army 2
	for i, placement := range army2 {
		ut := GetUnitType(placement.TypeName)
		if ut == nil {
			return nil, fmt.Errorf("unknown unit type: %s", placement.TypeName)
		}
		sim.Units = append(sim.Units, &Unit{
			ID:        fmt.Sprintf("p%d_u%d", player2ID, i),
			TypeName:  ut.Name,
			OwnerID:   player2ID,
			X:         placement.X,
			Y:         placement.Y,
			Health:    ut.Health,
			MaxHealth: ut.Health,
			Alive:     true,
		})
	}

	return sim, nil
}

// Step advances the simulation by one tick.
func (sim *Simulation) Step() {
	if sim.Status != "running" {
		return
	}
	sim.Tick++

	// Phase 1: Update projectiles
	sim.updateProjectiles()

	// Phase 2: Unit AI — find targets, move, attack
	sim.updateUnits()

	// Phase 3: Check win condition
	sim.checkWinCondition()
}

// updateProjectiles moves projectiles toward their targets and applies damage on arrival.
func (sim *Simulation) updateProjectiles() {
	alive := sim.Projectiles[:0]
	for _, proj := range sim.Projectiles {
		dist := Distance(proj.X, proj.Y, proj.TargetX, proj.TargetY)
		moveAmount := proj.Speed * DeltaTime

		if dist <= moveAmount {
			// Projectile arrived — apply damage
			sim.applyDamageAt(proj.TargetX, proj.TargetY, proj.Damage, proj.Splash, proj.OwnerID)
			continue // Remove projectile
		}

		// Move toward target
		dx := proj.TargetX - proj.X
		dy := proj.TargetY - proj.Y
		proj.X += (dx / dist) * moveAmount
		proj.Y += (dy / dist) * moveAmount
		alive = append(alive, proj)
	}
	sim.Projectiles = alive
}

// updateUnits handles unit AI: targeting, movement, and attacking.
func (sim *Simulation) updateUnits() {
	for _, unit := range sim.Units {
		if !unit.Alive {
			continue
		}

		ut := GetUnitType(unit.TypeName)
		if ut == nil {
			continue
		}

		// Healer: target nearest friendly that is damaged
		// Attacker: target nearest enemy
		isHealer := ut.AttackDamage < 0

		var target *Unit
		if isHealer {
			target = sim.findNearestFriendlyWounded(unit)
		} else {
			target = sim.findNearestEnemy(unit)
		}

		if target == nil {
			unit.TargetID = ""
			continue
		}
		unit.TargetID = target.ID

		dist := Distance(unit.X, unit.Y, target.X, target.Y)

		// Reduce cooldown
		unit.AttackCooldown -= DeltaTime
		if unit.AttackCooldown < 0 {
			unit.AttackCooldown = 0
		}

		if dist <= ut.AttackRange {
			// In range — attack if cooldown is ready
			if unit.AttackCooldown <= 0 {
				sim.executeAttack(unit, target, ut)
				unit.AttackCooldown = ut.AttackCooldown
			}
		} else {
			// Move toward target
			dx := target.X - unit.X
			dy := target.Y - unit.Y
			moveAmount := ut.Speed * DeltaTime
			unit.X += (dx / dist) * moveAmount
			unit.Y += (dy / dist) * moveAmount
		}
	}
}

// executeAttack fires an attack from unit to target.
func (sim *Simulation) executeAttack(attacker, target *Unit, ut *UnitType) {
	isHealer := ut.AttackDamage < 0

	if ut.ProjectileSpeed > 0 && !isHealer {
		// Ranged attack — spawn projectile
		sim.nextProjID++
		sim.Projectiles = append(sim.Projectiles, &Projectile{
			ID:      fmt.Sprintf("proj_%d", sim.nextProjID),
			OwnerID: attacker.OwnerID,
			X:       attacker.X,
			Y:       attacker.Y,
			TargetX: target.X,
			TargetY: target.Y,
			Speed:   ut.ProjectileSpeed,
			Damage:  ut.AttackDamage,
			Splash:  ut.SplashRadius,
		})
	} else if isHealer {
		// Healing: negative damage = positive healing
		healAmount := -ut.AttackDamage
		if ut.SplashRadius > 0 {
			// Area heal
			for _, u := range sim.Units {
				if !u.Alive || u.OwnerID != attacker.OwnerID {
					continue
				}
				if Distance(attacker.X, attacker.Y, u.X, u.Y) <= ut.SplashRadius+ut.AttackRange {
					u.Health += healAmount
					if u.Health > u.MaxHealth {
						u.Health = u.MaxHealth
					}
				}
			}
		} else {
			target.Health += healAmount
			if target.Health > target.MaxHealth {
				target.Health = target.MaxHealth
			}
		}
	} else {
		// Melee attack — direct damage
		if ut.SplashRadius > 0 {
			sim.applyDamageAt(target.X, target.Y, ut.AttackDamage, ut.SplashRadius, attacker.OwnerID)
		} else {
			target.Health -= ut.AttackDamage
			if target.Health <= 0 {
				target.Health = 0
				target.Alive = false
			}
		}
	}
}

// applyDamageAt deals damage at a location, optionally with splash radius.
func (sim *Simulation) applyDamageAt(x, y float64, damage int, splash float64, attackerOwnerID int) {
	if splash > 0 {
		for _, u := range sim.Units {
			if !u.Alive || u.OwnerID == attackerOwnerID {
				continue
			}
			if Distance(u.X, u.Y, x, y) <= splash {
				u.Health -= damage
				if u.Health <= 0 {
					u.Health = 0
					u.Alive = false
				}
			}
		}
	} else {
		// Single target: find the closest enemy unit to the impact point
		var closest *Unit
		minDist := math.MaxFloat64
		for _, u := range sim.Units {
			if !u.Alive || u.OwnerID == attackerOwnerID {
				continue
			}
			d := Distance(u.X, u.Y, x, y)
			if d < minDist {
				minDist = d
				closest = u
			}
		}
		if closest != nil && minDist < 1.0 {
			closest.Health -= damage
			if closest.Health <= 0 {
				closest.Health = 0
				closest.Alive = false
			}
		}
	}
}

// findNearestEnemy returns the closest alive enemy unit to the given unit.
func (sim *Simulation) findNearestEnemy(unit *Unit) *Unit {
	var closest *Unit
	minDist := math.MaxFloat64
	for _, u := range sim.Units {
		if !u.Alive || u.OwnerID == unit.OwnerID {
			continue
		}
		d := Distance(unit.X, unit.Y, u.X, u.Y)
		if d < minDist {
			minDist = d
			closest = u
		}
	}
	return closest
}

// findNearestFriendlyWounded returns the closest alive friendly unit that has taken damage.
func (sim *Simulation) findNearestFriendlyWounded(unit *Unit) *Unit {
	var closest *Unit
	minDist := math.MaxFloat64
	for _, u := range sim.Units {
		if !u.Alive || u.OwnerID != unit.OwnerID || u.Health >= u.MaxHealth {
			continue
		}
		d := Distance(unit.X, unit.Y, u.X, u.Y)
		if d < minDist {
			minDist = d
			closest = u
		}
	}
	return closest
}

// checkWinCondition checks if one side has been eliminated.
func (sim *Simulation) checkWinCondition() {
	owners := make(map[int]bool)
	for _, u := range sim.Units {
		if u.Alive {
			owners[u.OwnerID] = true
		}
	}

	if len(owners) <= 1 {
		sim.Status = "finished"
		for ownerID := range owners {
			sim.WinnerID = ownerID
		}
		// If no one is alive (mutual destruction), WinnerID stays 0 (draw)
	}
}

// GetState returns an immutable snapshot of the current simulation state.
func (sim *Simulation) GetState() BattleState {
	units := make([]Unit, len(sim.Units))
	for i, u := range sim.Units {
		units[i] = *u
	}

	projectiles := make([]Projectile, len(sim.Projectiles))
	for i, p := range sim.Projectiles {
		projectiles[i] = *p
	}

	return BattleState{
		Tick:        sim.Tick,
		Status:      sim.Status,
		Units:       units,
		Projectiles: projectiles,
		WinnerID:    sim.WinnerID,
	}
}

// AliveCount returns the number of alive units for a given owner.
func (sim *Simulation) AliveCount(ownerID int) int {
	count := 0
	for _, u := range sim.Units {
		if u.Alive && u.OwnerID == ownerID {
			count++
		}
	}
	return count
}

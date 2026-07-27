package analytics

import "math"

type scoreProfile struct {
	accuracyWeight   float64
	speedWeight      float64
	completionWeight float64
	targetReactionMS [3]int
}

// scoringProfiles is intentionally centralized. When the team finalizes each
// game's calibration, replace these domain defaults with game-specific values.
var scoringProfiles = map[Domain]scoreProfile{
	DomainAttention: {
		accuracyWeight:   0.65,
		speedWeight:      0.20,
		completionWeight: 0.15,
		targetReactionMS: [3]int{1800, 1500, 1200},
	},
	DomainProcessingSpeed: {
		accuracyWeight:   0.40,
		speedWeight:      0.50,
		completionWeight: 0.10,
		targetReactionMS: [3]int{1500, 1200, 900},
	},
	DomainExecutiveFunction: {
		accuracyWeight:   0.55,
		speedWeight:      0.20,
		completionWeight: 0.25,
		targetReactionMS: [3]int{3000, 2500, 2000},
	},
}

type scoreBreakdown struct {
	score      int
	accuracy   float64
	speed      float64
	completion float64
}

func calculateScore(p *SubmitSessionParams) scoreBreakdown {
	profile := scoringProfiles[p.Domain]
	answered := p.CorrectCount + p.WrongCount
	total := answered + p.OmittedCount

	accuracy := float64(p.CorrectCount) / float64(answered)
	completion := float64(answered) / float64(total)
	targetMS := profile.targetReactionMS[p.Difficulty-1]
	speed := clamp01(float64(targetMS) / float64(p.AverageReactionMS))

	weighted := profile.accuracyWeight*accuracy +
		profile.speedWeight*speed +
		profile.completionWeight*completion

	return scoreBreakdown{
		score:      int(math.Round(100 * clamp01(weighted))),
		accuracy:   round4(accuracy),
		speed:      round4(speed),
		completion: round4(completion),
	}
}

func clamp01(value float64) float64 {
	if value < 0 {
		return 0
	}
	if value > 1 {
		return 1
	}
	return value
}

func round4(value float64) float64 {
	return math.Round(value*10000) / 10000
}

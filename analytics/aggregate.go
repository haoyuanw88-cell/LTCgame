package analytics

import (
	"math"
	"sort"
	"time"
)

const (
	defaultRangeDays          = 30
	defaultTimezoneOffsetMins = 8 * 60
)

func buildSummary(playerID string, rangeDays int, sessions []gameSession, location *time.Location) *CognitiveSummary {
	domainScores := map[Domain][]int{
		DomainAttention:         {},
		DomainProcessingSpeed:   {},
		DomainExecutiveFunction: {},
	}
	effectiveDates := make(map[string]struct{})

	for _, session := range sessions {
		domainScores[session.Domain] = append(domainScores[session.Domain], session.Score)
		effectiveDates[dateKey(session.PlayedAt, location)] = struct{}{}
	}

	attention := averageInts(domainScores[DomainAttention])
	processing := averageInts(domainScores[DomainProcessingSpeed])
	executive := averageInts(domainScores[DomainExecutiveFunction])

	availableDomains := make([]int, 0, 3)
	for _, scores := range domainScores {
		if len(scores) > 0 {
			availableDomains = append(availableDomains, averageInts(scores))
		}
	}

	return &CognitiveSummary{
		PlayerID:               playerID,
		RangeDays:              rangeDays,
		EffectiveDays:          len(effectiveDates),
		RecordCount:            len(sessions),
		AttentionScore:         attention,
		ProcessingSpeedScore:   processing,
		ExecutiveFunctionScore: executive,
		OverallScore:           averageInts(availableDomains),
	}
}

func buildTrendPoints(sessions []gameSession, domain Domain, location *time.Location) ([]*TrendPoint, int) {
	type dailyBucket map[Domain][]int
	days := make(map[string]dailyBucket)
	sessionCounts := make(map[string]int)

	for _, session := range sessions {
		if domain != DomainOverall && session.Domain != domain {
			continue
		}
		key := dateKey(session.PlayedAt, location)
		if days[key] == nil {
			days[key] = make(dailyBucket)
		}
		days[key][session.Domain] = append(days[key][session.Domain], session.Score)
		sessionCounts[key]++
	}

	keys := make([]string, 0, len(days))
	for key := range days {
		keys = append(keys, key)
	}
	sort.Strings(keys)

	points := make([]*TrendPoint, 0, len(keys))
	for _, key := range keys {
		var score int
		if domain == DomainOverall {
			domainAverages := make([]int, 0, len(days[key]))
			for _, scores := range days[key] {
				domainAverages = append(domainAverages, averageInts(scores))
			}
			score = averageInts(domainAverages)
		} else {
			score = averageInts(days[key][domain])
		}
		points = append(points, &TrendPoint{
			Date:         key,
			Score:        score,
			SessionCount: sessionCounts[key],
		})
	}

	return points, len(keys)
}

func averageInts(values []int) int {
	if len(values) == 0 {
		return 0
	}
	total := 0
	for _, value := range values {
		total += value
	}
	return int(math.Round(float64(total) / float64(len(values))))
}

func dateKey(value time.Time, location *time.Location) string {
	return value.In(location).Format("2006-01-02")
}

func rangeStart(now time.Time, rangeDays int, location *time.Location) time.Time {
	localNow := now.In(location)
	startOfToday := time.Date(localNow.Year(), localNow.Month(), localNow.Day(), 0, 0, 0, 0, location)
	return startOfToday.AddDate(0, 0, -(rangeDays - 1)).UTC()
}

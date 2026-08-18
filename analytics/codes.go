package analytics

import "strings"

func compactGameCode(value string) string {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "stp", "stroop_color_match", "stroop_color":
		return "STP"
	case "ord", "number_order", "trail_making":
		return "ORD"
	case "sum", "number_sum":
		return "SUM"
	case "pip", "pipe_connection", "pipe_puzzle":
		return "PIP"
	case "crd", "card_memory_battle", "memory_cards":
		return "CRD"
	case "gop", "gopher_reaction", "body_whack_a_mole":
		return "GOP"
	case "sup", "supermarket_shopping", "supermarket":
		return "SUP"
	case "qiz", "true_false_life_quiz", "life_quiz":
		return "QIZ"
	default:
		return ""
	}
}

func compactDomainCode(value string) string {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "att", "attention_inhibition":
		return "ATT"
	case "spd", "processing_speed":
		return "SPD"
	case "exe", "executive_reasoning", "executive_function":
		return "EXE"
	case "vwm", "visual_working_memory":
		return "VWM"
	case "vsp", "visuospatial_planning":
		return "VSP"
	case "lng", "language":
		return "LNG"
	case "epm", "episodic_memory":
		return "EPM"
	case "ori", "orientation":
		return "ORI"
	case "mem", "memory":
		return "MEM"
	case "spr", "spatial_reasoning":
		return "SPR"
	case "mot", "motor_coordination":
		return "MOT"
	case "wel", "wellbeing":
		return "WEL"
	default:
		return ""
	}
}

func compactMetricCode(value string) string {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "vtc", "valid_trial_count": return "VTC"
	case "exr", "excluded_trial_rate": return "EXR"
	case "cpr", "completion_rate": return "CPR"
	case "acc", "accuracy": return "ACC"
	case "omr", "omission_rate": return "OMR"
	case "mrt", "median_correct_rt": return "MRT"
	case "mad", "rt_mad": return "MAD"
	case "rtv", "robust_rt_variability": return "RTV"
	case "ies", "inverse_efficiency": return "IES"
	case "tpi", "task_performance_index": return "TPI"
	case "lrt", "low_interference_median_rt": return "LRT"
	case "hrt", "high_interference_median_rt": return "HRT"
	case "sri", "stroop_rt_interference": return "SRI"
	case "ir", "interference_ratio": return "IR"
	case "sei", "stroop_error_interference": return "SEI"
	case "tct", "trail_total_completion_time": return "TCT"
	case "sec", "trail_sequence_error_count": return "SEC"
	case "crc", "trail_completed_round_count": return "CRC"
	case "rcr", "trail_round_completion_rate": return "RCR"
	case "osr", "planning_optimal_solution_rate": return "OSR"
	case "exm", "planning_median_excess_moves": return "EXM"
	case "pit", "planning_median_initial_thinking_time": return "PIT"
	case "ext", "planning_median_execution_time": return "EXT"
	case "rvc", "planning_rule_violation_count": return "RVC"
	case "pcr", "planning_round_completion_rate": return "PCR"
	default: return ""
	}
}

func compactConditionCode(value string) string {
	switch strings.ToLower(strings.TrimSpace(value)) {
	case "mlc", "match_low_conflict": return "MLC"
	case "xlc", "mismatch_low_conflict": return "XLC"
	case "mhc", "match_high_conflict": return "MHC"
	case "xhc", "mismatch_high_conflict": return "XHC"
	case "pos", "positive_only": return "POS"
	case "pan", "positive_and_negative": return "PAN"
	case "tsm", "target_sum": return "TSM"
	case "rsp", "response": return "RSP"
	case "rnd", "round_summary": return "RND"
	case "sel", "selection": return "SEL"
	default: return ""
	}
}

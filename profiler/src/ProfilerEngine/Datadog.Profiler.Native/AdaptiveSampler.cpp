#include "AdaptiveSampler.h"

#include <algorithm>
#include <stdexcept>

void AdaptiveSampler::Counts::AddTest()
{
	_testCount.fetch_add(1);
}

bool AdaptiveSampler::Counts::AddSample(int64_t limit)
{
	int64_t previousValue;
	int64_t newValue;

	do
	{
		previousValue = _sampleCount.load();
		newValue = min(previousValue + 1, limit);
	} while (!_sampleCount.compare_exchange_strong(previousValue, newValue));

	return newValue < limit;
}

void AdaptiveSampler::Counts::AddSample()
{
	_sampleCount.fetch_add(1);
}

void AdaptiveSampler::Counts::Reset()
{
	_testCount = 0;
	_sampleCount = 0;
}

int64_t AdaptiveSampler::Counts::SampleCount()
{
	return _sampleCount;
}

int64_t AdaptiveSampler::Counts::TestCount()
{
	return _testCount;
}

AdaptiveSampler::AdaptiveSampler(
	std::chrono::milliseconds windowDuration,
	int32_t samplesPerWindow,
	int32_t averageLookback,
	int32_t budgetLookback,
	std::function<void()> rollWindowCallback) :
	_timer([this] { RollWindow(); }, windowDuration),
	_rollWindowCallback(std::move(rollWindowCallback))
{
	if (averageLookback < 1)
	{
		throw std::invalid_argument("'averageLookback' argument must be at least 1");
	}

	if (budgetLookback < 1)
	{
		throw std::invalid_argument("'budgetLookback' argument must be at least 1");
	}

	_samplesPerWindow = samplesPerWindow;
	_budgetLookback = budgetLookback;

	_samplesBudget = samplesPerWindow + (static_cast<int64_t>(budgetLookback) * samplesPerWindow);
	_emaAlpha = ComputeIntervalAlpha(averageLookback);
	_budgetAlpha = ComputeIntervalAlpha(budgetLookback);

	_countsRef = &_countsSlots[0];

	// Initialize RNG
	std::random_device rd;
	_rng = std::mt19937(rd());
	_distribution = std::uniform_real_distribution<>(0.0, 1.0);

	_timer.Start();
}

bool AdaptiveSampler::Sample()
{
	auto* counts = _countsRef.load();
	counts->AddTest();

	if (NextDouble() < _probability)
	{
		return counts->AddSample(_samplesBudget);
	}

	return false;
}

bool AdaptiveSampler::Keep()
{
	auto* counts = _countsRef.load();
	counts->AddTest();
	counts->AddSample();
	return true;
}

bool AdaptiveSampler::Drop()
{
	auto* counts = _countsRef.load();
	counts->AddTest();
	return false;
}

double AdaptiveSampler::NextDouble()
{
	std::lock_guard lock(_rngMutex);
	return _distribution(_rng);
}

double AdaptiveSampler::ComputeIntervalAlpha(int32_t lookback)
{
	return 1 - pow(lookback, -1.0 / lookback);
}

int64_t AdaptiveSampler::CalculateBudgetEma(int64_t sampledCount)
{
	_avgSamples = isnan(_avgSamples) || _budgetAlpha <= 0.0
		? sampledCount
		: _avgSamples + _budgetAlpha * (sampledCount - _avgSamples);

	return llround(std::max(_samplesPerWindow - _avgSamples, 0.0) * _budgetLookback);
}

void AdaptiveSampler::RollWindow()
{
	auto& counts = _countsSlots[_countsSlotIndex];

	/*
	* Semi-atomically replace the Counts instance such that sample requests during window maintenance will be
	* using the newly created counts instead of the ones currently processed by the maintenance routine.
	* We are ok with slightly racy outcome where totaCount and sampledCount may not be totally in sync
	* because it allows to avoid contention in the hot-path and the effect on the overall sample rate is minimal
	* and will get compensated in the long run.
	* Theoretically, a compensating system might be devised but it will always require introducing a single point
	* of contention and add a fair amount of complexity. Considering that we are ok with keeping the target sampling
	* rate within certain error margins and this data race is not breaking the margin it is better to keep the
	* code simple and reasonably fast.
	*/

	_countsSlotIndex = (_countsSlotIndex + 1) % 2;
	_countsRef = &_countsSlots[_countsSlotIndex];
	const auto totalCount = counts.TestCount();
	const auto sampledCount = counts.SampleCount();

	_samplesBudget = CalculateBudgetEma(sampledCount);

	if (_totalCountRunningAverage == 0 || _emaAlpha <= 0.0)
	{
		_totalCountRunningAverage = totalCount;
	}
	else
	{
		_totalCountRunningAverage = _totalCountRunningAverage + _emaAlpha * (totalCount - _totalCountRunningAverage);
	}

	if (_totalCountRunningAverage <= 0)
	{
		_probability = 1;
	}
	else
	{
		_probability = min(_samplesBudget / _totalCountRunningAverage, 1.0);
	}

	counts.Reset();

	if (_rollWindowCallback != nullptr)
	{
		_rollWindowCallback();
	}
}

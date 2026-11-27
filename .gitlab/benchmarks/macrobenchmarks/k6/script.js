import { check } from "k6";
import http from "k6/http";

const SCENARIO = __ENV.SCENARIO;
const EXTENDED_SCENARIO_NAME = __ENV.EXTENDED_SCENARIO_NAME;
const ENDPOINT = __ENV.ENDPOINT

const scenarios = {
  warmup: {
    executor: "constant-vus",
    duration: __ENV.K6_OPTIONS_WARMUP_DURATION,
    gracefulStop: __ENV.K6_OPTIONS_WARMUP_GRACEFUL_STOP,
    vus: parseInt(__ENV.K6_OPTIONS_WARMUP_VUS || __ENV.K6_OPTIONS_WARMUP_MAX_VUS),
  },
  normal_operation: {
    executor: "constant-arrival-rate",
    rate: parseInt(__ENV.K6_OPTIONS_NORMAL_OPERATION_RATE),
    timeUnit: "1s",
    duration: __ENV.K6_OPTIONS_NORMAL_OPERATION_DURATION,
    gracefulStop: __ENV.K6_OPTIONS_NORMAL_OPERATION_GRACEFUL_STOP,
    preAllocatedVUs: parseInt(
        __ENV.K6_OPTIONS_NORMAL_OPERATION_PRE_ALLOCATED_VUS,
    ),
    maxVUs: parseInt(__ENV.K6_OPTIONS_NORMAL_OPERATION_MAX_VUS),
  },
  high_load: {
    executor: "constant-vus",
    duration: __ENV.K6_OPTIONS_HIGH_LOAD_DURATION,
    gracefulStop: __ENV.K6_OPTIONS_HIGH_LOAD_GRACEFUL_STOP,
    vus: parseInt(__ENV.K6_OPTIONS_HIGH_LOAD_VUS || __ENV.K6_OPTIONS_HIGH_LOAD_MAX_VUS),
  },
};

export const options = {
  summaryTimeUnit: 'us',
  scenarios: {
    [EXTENDED_SCENARIO_NAME]: scenarios[SCENARIO],
  },
};

export default function () {
  const res = http.get("http://localhost:8080/"+ENDPOINT);

  check(res, {
    "is status 200": (r) => r.status === 200,
  });
}

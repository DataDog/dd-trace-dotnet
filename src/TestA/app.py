import sentry_sdk
import time

# Initialize Sentry with the DSN provided
sentry_sdk.init(dsn="https://320272b8770f9a91f14e6702aec9563a@o4507172871340032.ingest.us.sentry.io/4507172872847360")

def throw_exception():
    while True:
        try:
            # This line will throw an exception
            raise Exception("This is a test exception from TestA")
        except Exception as e:
            # Capture the exception in Sentry
            sentry_sdk.capture_exception(e)
            print("Exception captured in Sentry.")
        # Wait for 5 seconds before throwing the next exception
        time.sleep(5)

if __name__ == "__main__":
    throw_exception()

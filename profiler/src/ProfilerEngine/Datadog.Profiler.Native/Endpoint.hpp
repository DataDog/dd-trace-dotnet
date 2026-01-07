#include <string>
#include <memory>

namespace libdatadog
{
    class Endpoint
    {
    public:
        
        static Endpoint CreateAgentEndpoint();
        static Endpoint CreateFileEndpoint(std::string path);
    private:
        Endpoint();

        struct EndpointImpl;
        std::unique_ptr<EndpointImpl> _endpoint;
    };
};